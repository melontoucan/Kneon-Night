using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using Yukar.Common.GameData;
using Yukar.Common.Rom;
using Yukar.Engine;

namespace Bakin
{
    public class WomboCombo : BakinObject
    {
        private SystemData bakinSystem;

        private string motionData = "motionData";
        private string startingAttack = "startingAttack";
        private string disableAttack = "cantAttack";
        private string comboUpdate = "updateCombo";
        private string cleanArray = "cleanArray";

        private string comboSwitch = "comboStart";

        List<SpecialMotion> motionCastList = new List<SpecialMotion>();

        private bool ltAttack;
        private bool changeState;
        private int ltAttackState = -1;
        private int lastLtAttackState;
        private bool startTimer;
        private bool isAttacking;
        private ScriptRunner currentCommonEvent;
        private Guid chunkId = new Guid("6642815F-600D-4789-A081-A3993B2076CA");
        private float time;


        //  private string attackName = "BasicAttk";

        public override void Start()
        {
            bakinSystem = mapScene.owner.data.system;
            // PrepareGeneratedEvent();

            // キャラクターが生成される時に、このメソッドがコールされます。
            // This method is called when the character is created.
        }

        //private void PrepareGeneratedEvent()
        //{
        //    Script.Command newCommands = new Script.Command
        //    {
        //        type = Script.Command.FuncType.SHOT_EVENT
        //    };

        //    var cast = catalog.getItemFromName(attackName, typeof(Cast)) as Cast;

        //    GameMain.PushLog(DebugDialog.LogEntry.LogType.EVENT, "ComboWombo", cast.getSourceEvent(catalog).guId.ToString());

        //    newCommands.attrList.Add(new Script.GuidAttr() { value = cast.guId }); // cast guid
        //    newCommands.attrList.Add(new Script.IntAttr { value = 0 });           // source player
        //    newCommands.attrList.Add(new Script.IntAttr() { value = 1 });
        //    newCommands.attrList.Add(new Script.FloatAttr() { value = 180 });
        //    newCommands.attrList.Add(new Script.IntAttr() { value = 1 });
        //    newCommands.attrList.Add(new Script.FloatAttr() { value = 0f });
        //    newCommands.attrList.Add(new Script.FloatAttr() { value = 0f });
        //    newCommands.attrList.Add(new Script.FloatAttr() { value = 0f });
        //    newCommands.attrList.Add(new Script.IntAttr { value = 0 });

        //    List<Script.Command> commandList = new List<Script.Command>
        //    {
        //        newCommands
        //    };

        //    Script script = new Script()
        //    {
        //        Name = "test",
        //        trigger = Script.Trigger.AUTO_PARALLEL,
        //        commands = commandList
        //    };
        //    eventRunner = new ScriptRunner(mapScene, mapChr, script);
        //}

        public override void Update()
        {
            // if (!bakinSystem.GetSwitch(comboSwitch)) return;
            ComboUpdate();

            if (!PlayerAvailable()) return;
            InputChecker();
            StateManager();
            ExecuteMotion();
            FinalizerManager();
            Timer();


            // キャラクターが生存している間、
            // 毎フレームこのキャラクターのアップデート前にこのメソッドがコールされます。
            // This method is called every frame before this character updates while the character is alive.
        }

        private bool PlayerAvailable()
        {
            if (bakinSystem.GetSwitch(disableAttack, Guid.Empty, false))
            {
                ltAttack = false;
                lastLtAttackState = -1;
                changeState = false;
                isAttacking = false;
                time = 0;
                return false;
            }

            return true;
        }

        private void ComboUpdate(bool force = false)
        {

            if (!bakinSystem.GetSwitch(comboUpdate) && !force && !bakinSystem.GetSwitch(cleanArray)) return;

            var arrays = bakinSystem.VariableArrays;

            if (!arrays.ContainsKey(motionData))
            {
                bakinSystem.SetToArray(motionData, 0, "");

                GameMain.PushLog(DebugDialog.LogEntry.LogType.EVENT, "ComboWombo", "no contiene la key");
                return;
            }

            var arrayValues = arrays[motionData].values;

            if (arrayValues == null) return;

            motionCastList.Clear();
            if (bakinSystem.GetSwitch(cleanArray, Guid.Empty, false))
            {
                for (int i = 0; i < arrayValues.Count; i++)
                {
                    bakinSystem.SetToArray(motionData, i, "");
                }
            }
            else
            {
                foreach (var motionDataDic in arrayValues)
                {

                    var stringData = motionDataDic.Value.getString();
                    GameMain.PushLog(DebugDialog.LogEntry.LogType.EVENT, "ComboWombo", stringData);
                    if (string.IsNullOrEmpty(stringData)) continue;

                    var nameTiming = stringData.Split(',');

                    if (nameTiming.Length != 2 || !float.TryParse(nameTiming[1], out float timing)) continue;

                    SpecialMotion motionToAdd = new SpecialMotion()
                    {
                        MotionName = nameTiming[0],
                        MotionTiming = timing
                    };

                    motionCastList.Add(motionToAdd);

                    //  GameMain.PushLog(DebugDialog.LogEntry.LogType.EVENT, "ComboWombo", motionToAdd.MotionName);
                }
            }



            bakinSystem.SetSwitch(cleanArray, false, Guid.Empty, false);
            //   GameMain.PushLog(DebugDialog.LogEntry.LogType.EVENT, "ComboWombo", "wtf");
            bakinSystem.SetSwitch(comboUpdate, false);
        }

        private void Timer()
        {
            if (!startTimer) return;
            time += GameMain.getElapsedTime();
            if (time >= motionCastList[ltAttackState].MotionTiming)
            {
                time = 0;
                startTimer = false;
                ShotEvent();
            }
        }

        private void ShotEvent()
        {
            RunCommonEvent("Wombo");
            // eventRunner.state = 0;
            // eventRunner.Run();
        }

        private void FinalizerManager()
        {
            var hero = mapScene.GetHero();
            if (hero == null) return;
            if (hero.getModelInstance().getMotionLoopCount() != 0 && isAttacking)
            {
                hero.playMotion("Wait", 0.2f, false, false);
                mapScene.UnlockControl();
                isAttacking = false;
            }
        }

        private void ExecuteMotion()
        {
            if (isAttacking || !ltAttack || motionCastList.Count == 0) return;
            var hero = mapScene.GetHero();
            mapScene.LockControl();
            bakinSystem.SetSwitch(startingAttack, true); // starting attack notifier
            hero.playMotion(motionCastList[ltAttackState].MotionName, 0.2f, false, true);
            GameMain.PushLog(DebugDialog.LogEntry.LogType.EVENT, "ComboWombo", "Se deberia ejecutar: " + motionCastList[ltAttackState].MotionName);
            startTimer = true;
            isAttacking = true;
            ltAttack = false;
            return;
        }

        private void StateManager()
        {

            if (!changeState || isAttacking) return;
            // In case of more inputs
            if (ltAttack) changeLTstate();

            changeState = false;
        }

        private void changeLTstate()
        {
            lastLtAttackState = ltAttackState;

            ltAttackState++;

            if (ltAttackState >= motionCastList.Count) ltAttackState = 0;
            if (ltAttackState + 1 > lastLtAttackState) ltAttackState = lastLtAttackState + 1;

            mapScene.owner.data.system.SetVariable("comboIndex", ltAttackState);
        }

        private void ExitCombo()
        {
            mapScene.UnlockControl();
        }

        private void InputChecker()
        {
            if (Input.KeyTest(Input.StateType.TRIGGER, Input.KeyStates.CAMERA_ZOOM_IN, Input.GameState.WALK)) // Left attack
            {

                GameMain.PushLog(DebugDialog.LogEntry.LogType.EVENT, "dadas", "DEBERIA ATACARRR");
                ltAttack = true;
                changeState = true;
            }
        }
        public override void BeforeUpdate()
        {
            // キャラクターが生存している間、
            // 毎フレーム、イベント内容の実行前にこのメソッドがコールされます。
            // This method will be called every frame while the character is alive, before the event content is executed.
        }

        public override void Destroy()
        {
            // キャラクターが破棄される時に、このメソッドがコールされます。
            // This method is called when the character is destroyed.
        }

        public override void AfterDraw()
        {
            // このフレームの2D描画処理の最後に、このメソッドがコールされます。
            // This method is called at the end of the 2D drawing process for this frame.
        }

        private void ChunkOperations()
        {
            var arrays = bakinSystem.VariableArrays;

            var arrayValues = arrays[motionData].values;

            foreach (var motionData in arrayValues)
            {

                GameMain.PushLog(DebugDialog.LogEntry.LogType.EVENT, "ComboWombo", motionData.Value.getString());
                //  if (tempMotion == null || motionCastList.Contains(tempMotion)) continue;
                //  motionCastList.Add(tempMotion);
            }
        }
        private void RunCommonEvent(string commonEventName)
        {
            if (string.IsNullOrEmpty(commonEventName)) return;

            commonEventName = commonEventName.ToLower();
            var chr = mapScene.mapCharList.FirstOrDefault(x => x.rom?.name.ToLower() == commonEventName);

            if (chr != null)
            {
                currentCommonEvent = mapScene.GetScriptRunner(chr.guId);
                currentCommonEvent?.Run();
            }


        }

        internal class SpecialMotion
        {
            private string motionName;
            private float motionTiming;

            public string MotionName { get => motionName; set => motionName = value; }
            public float MotionTiming { get => motionTiming; set => motionTiming = value; }
        }
    }
}
