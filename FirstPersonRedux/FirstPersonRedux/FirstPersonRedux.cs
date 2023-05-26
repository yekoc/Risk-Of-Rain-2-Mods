using BepInEx;
using BepInEx.Configuration;
using RoR2;
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
    [BepInPlugin("xyz.yekoc.FirstPersonRedux", "First Person Redux","1.1.0" )]
    [BepInDependency("com.rune580.riskofoptions",BepInDependency.DependencyFlags.SoftDependency)]
    public class FirstPersonReduxPlugin : BaseUnityPlugin
    {
	public static ConfigEntry<KeyboardShortcut> keyConfig;
        public static ConfigEntry<bool> isFirstPersonInternal;
        public static KeyboardShortcut toggleKey;
        public static CameraTargetParams.CameraParamsOverrideHandle overrideHandle;
        public static CharacterCameraParamsData data; 
	private void Awake(){
            keyConfig = Config.Bind<KeyboardShortcut>("Inputs","Toggle First Person",new KeyboardShortcut(UnityEngine.KeyCode.Keypad5),"Toggle First Person Camera");
            keyConfig.SettingChanged += (sender,data) => toggleKey = ((ConfigEntry<KeyboardShortcut>)sender).Value;
            isFirstPersonInternal = Config.Bind<bool>("Gameplay","Hide Body",false,"Whether the target body is hidden when in first person");
            toggleKey = keyConfig.Value;
            On.RoR2.PlayerCharacterMasterController.Update += CheckButtonHook;
            On.EntityStates.Toolbot.ToolbotDualWieldBase.OnEnter += PowerPersonMode;
            On.RoR2.SceneExitController.SetState += PreservePoVOnSceneChange;
            if(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions")){
                RoOptionize();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void RoOptionize(){
            RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.KeyBindOption(keyConfig));
            RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(isFirstPersonInternal));
        }
        private void OnDestroy(){
            On.RoR2.PlayerCharacterMasterController.Update -= CheckButtonHook;
            On.EntityStates.Toolbot.ToolbotDualWieldBase.OnEnter -= PowerPersonMode;
            On.RoR2.SceneExitController.SetState -= PreservePoVOnSceneChange;
        }

        internal static void PreservePoVOnSceneChange(On.RoR2.SceneExitController.orig_SetState orig,SceneExitController self,SceneExitController.ExitState state){
            if(state == SceneExitController.ExitState.TeleportOut && overrideHandle.isValid){
                PlayerCharacterMasterController.instances[0].master.onBodyStart += passItForward;
            }
            orig(self,state);
            void passItForward(CharacterBody bod){
              var cameratarget = bod.gameObject.GetComponent<CameraTargetParams>();
              data = cameratarget.currentCameraParamsData;
              data.idealLocalCameraPos = new Vector3(0,0,0);
              data.isFirstPerson = isFirstPersonInternal.Value;
              overrideHandle = cameratarget.AddParamsOverride(new CameraTargetParams.CameraParamsOverrideRequest{
                      cameraParamsData = data,
                      priority = 0.5f
              });
              bod.master.onBodyStart -= passItForward;
            }
        }
        internal static void PowerPersonMode(On.EntityStates.Toolbot.ToolbotDualWieldBase.orig_OnEnter orig,EntityStates.Toolbot.ToolbotDualWieldBase self){
            self.applyCameraAimMode = !overrideHandle.isValid;
            orig(self);
        }
        internal static void CheckButtonHook(On.RoR2.PlayerCharacterMasterController.orig_Update orig, PlayerCharacterMasterController self)
        {
            if(toggleKey.IsDown()){
                if(!overrideHandle.isValid){
                    data = self.body.gameObject.GetComponent<CameraTargetParams>().currentCameraParamsData;
                    data.idealLocalCameraPos = new Vector3(0,0,0);
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
