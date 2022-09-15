using BepInEx;
using BepInEx.Configuration;
using RoR2;
using System.Collections.Generic;
using System.Security;
using System.Security.Permissions;
using System.Runtime.CompilerServices;
using UnityEngine;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace FirstPersonRedux
{
    [BepInPlugin("xyz.yekoc.FirstPersonRedux", "First Person Redux","1.0.0" )]
    [BepInDependency("com.rune580.riskofoptions",BepInDependency.DependencyFlags.SoftDependency)]
    public class FirstPersonReduxPlugin : BaseUnityPlugin
    {
	public static ConfigEntry<KeyboardShortcut> keyConfig;
        public static ConfigEntry<bool> isFirstPersonInternal;
        public static ConfigEntry<bool> smartPlacement;
        public static KeyboardShortcut toggleKey;
        public static CameraTargetParams.CameraParamsOverrideHandle overrideHandle;
        public static CharacterCameraParamsData data;
        public static Dictionary<BodyIndex,Transform> headPos = new();

	private void Awake(){
            keyConfig = Config.Bind<KeyboardShortcut>("Inputs","Toggle First Person",new KeyboardShortcut(UnityEngine.KeyCode.Keypad5),"Toggle First Person Camera");
            keyConfig.SettingChanged += (sender,data) => toggleKey = ((ConfigEntry<KeyboardShortcut>)sender).Value;
            isFirstPersonInternal = Config.Bind<bool>("Gameplay","Hide Body",false,"Whether the target body is hidden when in first person");
            smartPlacement = Config.Bind<bool>("Gameplay","Smart Camera Placement",true,"When enabled the camera attempts to find the model's \"Head\",disable to use standard first person position.");
            toggleKey = keyConfig.Value;
            On.RoR2.PlayerCharacterMasterController.FixedUpdate += CheckButtonHook;
            CharacterBody.onBodyStartGlobal += OnBodyStart;
            On.RoR2.PlayerCharacterMasterController.Update += UpdateSmartPosition;
            if(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions")){
                RoOptionize();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void RoOptionize(){
            RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.KeyBindOption(keyConfig));
            RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(isFirstPersonInternal));
            RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(smartPlacement));
        }
        private void OnDestroy(){
            On.RoR2.PlayerCharacterMasterController.FixedUpdate -= CheckButtonHook;
            CharacterBody.onBodyStartGlobal -= OnBodyStart;
            On.RoR2.PlayerCharacterMasterController.Update -= UpdateSmartPosition;
        }

        private void OnBodyStart(CharacterBody body){
            if(headPos.ContainsKey(body.bodyIndex)){
                return;
            }
            var model = body.GetComponent<CharacterModel>();
            if(model){
                var head = model.childLocator.FindChild("Head");
                if(!head){
                    head = model.childLocator.FindChild("HeadCenter");
                }
                if(head)
                  headPos.Add(body.bodyIndex,head);
            }
        }

        private void UpdateSmartPosition(On.RoR2.PlayerCharacterMasterController.orig_Update orig,PlayerCharacterMasterController self){
            orig(self);
            if(smartPlacement.Value && overrideHandle.isValid && headPos.ContainsKey(self.body.bodyIndex)){
              data.idealLocalCameraPos = headPos[self.body.bodyIndex].position;
            }
        }

        internal static void CheckButtonHook(On.RoR2.PlayerCharacterMasterController.orig_FixedUpdate orig, PlayerCharacterMasterController self)
        {
            if(toggleKey.IsDown()){
                if(!overrideHandle.isValid){
                    data = self.body.gameObject.GetComponent<CameraTargetParams>().currentCameraParamsData;
                    data.idealLocalCameraPos = (smartPlacement.Value && headPos.ContainsKey(self.body.bodyIndex))? headPos[self.body.bodyIndex].position : new Vector3(0,0,0);
                    data.isFirstPerson = isFirstPersonInternal.Value;
                    overrideHandle = self.body.gameObject.GetComponent<CameraTargetParams>().AddParamsOverride(new CameraTargetParams.CameraParamsOverrideRequest{
                        cameraParamsData = data,
                        priority = 0.5f
                    });
                    self.master.onBodyDestroyed += cleanup;
                }
                else{
                  self.body.gameObject.GetComponent<CameraTargetParams>().RemoveParamsOverride(overrideHandle);
                  overrideHandle = default;
                }
            }
            orig(self);
            void cleanup(CharacterBody bod){
                if(overrideHandle.isValid){
                 overrideHandle = default; 
                }
                bod.master.onBodyDestroyed -= cleanup;
            }
        }
    }
}
