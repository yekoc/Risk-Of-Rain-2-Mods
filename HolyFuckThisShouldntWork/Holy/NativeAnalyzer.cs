using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace HolyHolyHoly
{
    // Conservative call-graph walk answering "can calling this method end up somewhere a worker thread must not go?"
    // That means Unity native code (InternalCall/PInvoke outside the BCL), delegate invocations we can't see through,
    // and writes to static fields (shared scratch state races between workers).
    // Pure System.Reflection on purpose so the same file can be pointed at the game assemblies offline (see Tests/).
    internal sealed class NativeAnalyzer
    {
        public sealed class Verdict
        {
            public static readonly Verdict Safe = new Verdict(false, null);
            public readonly bool Unsafe;
            public readonly string Reason;
            public Verdict(bool isUnsafe, string reason){ Unsafe = isUnsafe; Reason = reason; }
            public static Verdict No(string reason) => new Verdict(true, reason);
        }

        // Return non-null to short-circuit analysis of a method (whitelists, hook-awareness, handled call sites).
        public Func<MethodBase, Verdict> Override;
        // Additional methods that may run when this one is called (virtual overrides, On hooks, Harmony patches).
        public Func<MethodBase, IEnumerable<MethodBase>> ExtraTargets;
        // Every type whose members a scanned method touches, so static constructors can be forced on the main thread.
        public Action<Type> OnTypeTouched;
        public int MaxDepth = 48;

        readonly Dictionary<string, Verdict> memo = new Dictionary<string, Verdict>();
        readonly Dictionary<string, int> onStack = new Dictionary<string, int>();

        public void Clear(){ memo.Clear(); onStack.Clear(); }

        public Verdict Analyze(MethodBase method){
            onStack.Clear();
            return Visit(method, 0, out _);
        }

        Verdict Visit(MethodBase m, int depth, out int low){
            low = int.MaxValue;
            string key = Key(m);
            if(memo.TryGetValue(key, out var known)) return known;
            if(onStack.TryGetValue(key, out int stackDepth)){
                // Recursion: provisionally safe, but nothing above this frame may be memoized as safe until it unwinds.
                low = stackDepth;
                return Verdict.Safe;
            }
            if(depth > MaxDepth){
                var deep = Verdict.No("call chain too deep to verify at " + Describe(m));
                memo[key] = deep;
                return deep;
            }
            var quick = Classify(m);
            if(quick != null){
                memo[key] = quick;
                return quick;
            }
            onStack[key] = depth;
            Verdict result = Verdict.Safe;
            try{
                foreach(var callee in Callees(m)){
                    var r = Visit(callee, depth + 1, out int calleeLow);
                    if(calleeLow < low) low = calleeLow;
                    if(r.Unsafe){
                        result = Verdict.No(Describe(m) + " -> " + r.Reason);
                        break;
                    }
                }
                if(!result.Unsafe && ExtraTargets != null){
                    foreach(var extra in ExtraTargets(m)){
                        if(extra == null) continue;
                        var r = Visit(extra, depth + 1, out int calleeLow);
                        if(calleeLow < low) low = calleeLow;
                        if(r.Unsafe){
                            result = Verdict.No(Describe(m) + " => " + r.Reason);
                            break;
                        }
                    }
                }
            }
            catch(StaticWriteException e){
                result = Verdict.No(Describe(m) + " -> " + e.Message);
            }
            catch(Exception e){
                result = Verdict.No("could not analyze " + Describe(m) + " (" + e.GetType().Name + ": " + e.Message + ")");
            }
            onStack.Remove(key);
            if(low >= depth) low = int.MaxValue;
            if(result.Unsafe || low == int.MaxValue) memo[key] = result;
            return result;
        }

        Verdict Classify(MethodBase m){
            var o = Override?.Invoke(m);
            if(o != null) return o;
            var type = m.DeclaringType;
            string asm = type?.Assembly.GetName().Name ?? "";
            string typeName = type?.FullName ?? "";
            if(IsWhitelisted(typeName, m.Name)) return Verdict.Safe;
            bool bcl = IsBcl(asm);
            var impl = m.MethodImplementationFlags;
            if((impl & MethodImplAttributes.InternalCall) != 0 || (m.Attributes & MethodAttributes.PinvokeImpl) != 0){
                return bcl ? Verdict.Safe : Verdict.No("native " + Describe(m));
            }
            if((impl & MethodImplAttributes.CodeTypeMask) == MethodImplAttributes.Runtime){
                // Delegate Invoke: the target is invisible here, only the call sites that load the delegate know it.
                if(!m.IsConstructor && m.Name != "Invoke") return Verdict.Safe;
                return m.IsConstructor ? Verdict.Safe : Verdict.No("opaque delegate call " + Describe(m));
            }
            if(bcl) return Verdict.Safe;
            if(m.IsAbstract) return Verdict.Safe;
            return null;
        }

        public static bool IsBcl(string asm) =>
            asm == "mscorlib" || asm == "netstandard" || asm == "System" || asm.StartsWith("System.", StringComparison.Ordinal) || asm == "Microsoft.CSharp";

        public static bool IsUnity(string asm) =>
            asm.StartsWith("UnityEngine", StringComparison.Ordinal) || asm.StartsWith("Unity.", StringComparison.Ordinal);

        static bool IsWhitelisted(string type, string name){
            switch(type){
                // Pure math, including the handful of native helpers; all reentrant.
                case "UnityEngine.Mathf":
                case "UnityEngine.Vector2":
                case "UnityEngine.Vector3":
                case "UnityEngine.Vector4":
                case "UnityEngine.Vector2Int":
                case "UnityEngine.Vector3Int":
                case "UnityEngine.Quaternion":
                case "UnityEngine.Matrix4x4":
                case "UnityEngine.Color":
                case "UnityEngine.Color32":
                case "UnityEngine.Bounds":
                case "UnityEngine.Rect":
                    return true;
                // Unity's logger is documented as thread-safe.
                case "UnityEngine.Debug":
                    return name.StartsWith("Log", StringComparison.Ordinal) || name.StartsWith("Assert", StringComparison.Ordinal);
                case "UnityEngine.Object":
                    // Null/lifetime checks only read the cached native pointer for live objects.
                    // Destroyed non-script objects fall back to an instance-id lookup, which is a read.
                    return name == "op_Implicit" || name == "op_Equality" || name == "op_Inequality" || name == "CompareBaseObjects"
                        || name == "IsNativeObjectAlive" || name == "GetCachedPtr" || name == "Equals" || name == "GetHashCode";
                // Only touch Object.name when the def never got registered in its catalog, which can't happen to anything
                // vanilla references (and modded defs are only looked up from GetStatCoefficients, which runs on the main thread).
                case "RoR2.ItemDef":
                    return name == "get_itemIndex";
                case "RoR2.EquipmentDef":
                    return name == "get_equipmentIndex";
                // Lazily resolves the master with GetComponent; the batch warms this cache on the main thread first.
                case "RoR2.CharacterBody":
                    return name == "get_master";
            }
            return false;
        }

        IEnumerable<MethodBase> Callees(MethodBase m){
            var body = m.GetMethodBody();
            if(body == null) yield break;
            byte[] il = body.GetILAsByteArray();
            if(il == null) yield break;
            var module = m.Module;
            Type[] typeArgs = (m.DeclaringType != null && m.DeclaringType.IsGenericType) ? m.DeclaringType.GetGenericArguments() : null;
            Type[] methodArgs = (m.IsGenericMethod && !m.IsConstructor) ? m.GetGenericArguments() : null;
            int i = 0;
            while(i < il.Length){
                OpCode op;
                byte b = il[i++];
                if(b == 0xFE) op = TwoByte[il[i++]];
                else op = OneByte[b];
                int operandSize = OperandSize(op.OperandType, il, i);
                if(op.OperandType == OperandType.InlineMethod){
                    int token = BitConverter.ToInt32(il, i);
                    MethodBase target = null;
                    try{ target = module.ResolveMethod(token, typeArgs, methodArgs); }
                    catch(Exception){ }
                    if(target == null) throw new InvalidOperationException("unresolvable method token 0x" + token.ToString("X8"));
                    if(target.DeclaringType != null) OnTypeTouched?.Invoke(target.DeclaringType);
                    yield return target;
                }
                else if(op.OperandType == OperandType.InlineField){
                    int token = BitConverter.ToInt32(il, i);
                    FieldInfo field = null;
                    try{ field = module.ResolveField(token, typeArgs, methodArgs); }
                    catch(Exception){ }
                    if(field != null && field.IsStatic){
                        if(field.DeclaringType != null) OnTypeTouched?.Invoke(field.DeclaringType);
                        bool write = op.Value == OpCodes.Stsfld.Value || (op.Value == OpCodes.Ldsflda.Value && !field.IsInitOnly);
                        if(write && field.GetCustomAttributes(typeof(ThreadStaticAttribute), false).Length == 0){
                            string asm = field.DeclaringType?.Assembly.GetName().Name ?? "";
                            if(!IsBcl(asm)) throw new StaticWriteException(field);
                        }
                    }
                }
                i += operandSize;
            }
        }

        // Thrown from the IL walk so the visitor reports it like any other unsafe finding.
        sealed class StaticWriteException : Exception
        {
            public StaticWriteException(FieldInfo f) : base("writes shared static " + f.DeclaringType?.Name + "." + f.Name){ }
        }

        static int OperandSize(OperandType t, byte[] il, int at){
            switch(t){
                case OperandType.InlineNone: return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: return 1;
                case OperandType.InlineVar: return 2;
                case OperandType.InlineI8:
                case OperandType.InlineR: return 8;
                case OperandType.InlineSwitch: return 4 + 4 * BitConverter.ToInt32(il, at);
                default: return 4;
            }
        }

        static OpCode[] oneByte, twoByte;
        static OpCode[] OneByte { get { if(oneByte == null) BuildTables(); return oneByte; } }
        static OpCode[] TwoByte { get { if(twoByte == null) BuildTables(); return twoByte; } }
        static void BuildTables(){
            var one = new OpCode[256];
            var two = new OpCode[256];
            foreach(var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)){
                if(!(f.GetValue(null) is OpCode op)) continue;
                ushort v = (ushort)op.Value;
                if(op.Size == 1) one[v] = op;
                else if((v >> 8) == 0xFE) two[v & 0xFF] = op;
            }
            twoByte = two;
            oneByte = one;
        }

        public static string Describe(MethodBase m) => (m.DeclaringType?.Name ?? "?") + "." + m.Name;

        static string Key(MethodBase m){
            string k = m.Module.ModuleVersionId.ToString("N") + ":" + m.MetadataToken.ToString("X");
            if(m.DeclaringType != null && m.DeclaringType.IsGenericType) k += "<" + string.Join(",", Array.ConvertAll(m.DeclaringType.GetGenericArguments(), t => t.FullName ?? t.Name)) + ">";
            if(m.IsGenericMethod && !m.IsConstructor) k += "[" + string.Join(",", Array.ConvertAll(m.GetGenericArguments(), t => t.FullName ?? t.Name)) + "]";
            return k;
        }
    }
}
