using System;
using System.Collections.Generic;
using Yukar.Common.GameData;
using Yukar.Engine;
using static Yukar.Engine.MapData;

namespace Bakin
{
    public class GeneratedChecker : BakinObject
    {
        string playerAttackTag = "$playerattack";
        string enemyAttackTag = "$enemyattack";

        string canTakeDamageTag = "$idamage";


        string fatherName = string.Empty;
        private Guid fatherGuid;

        string strAttack = "scAttack";
        string strDefense = "scDefense";
        string strConstant = "scConstant";
        private bool fatherFound;
        private bool collidesWithPlayer;
        private bool dataInitialized;
        private bool collidesWithEvents;
        private List<MapCharacter> filteredColliding = new List<MapCharacter>();
        private string strDamage = "scDamage";
        Random rnd = new Random();

        public override void Start()
        {
            // キャラクターが生成される時に、このメソッドがコールされます。
            // This method is called when the character is created.
        }

        public override void Update()
        {




            // キャラクターが生存している間、
            // 毎フレームこのキャラクターのアップデート前にこのメソッドがコールされます。
            // This method is called every frame before this character updates while the character is alive.
        }


        public override void BeforeUpdate()
        {
            if (mapScene == null)
            {
                if (mapChr == null) return;
                mapScene = GameMain.instance.mapScene;
                return;
            }

            if (!mapChr.IsDynamicGenerated) return;

            mapChr.updateColliderPositions();


            var tags = mapChr.rom.Cast.tags.ToLower();

            if (tags.Contains(playerAttackTag))
            {
                // execute player attack logic to give damage to every single event that this event collides
                StartPlayerAttack();
            }
            else if (tags.Contains(enemyAttackTag))
            {
                // here will be the enemy attack logic to give damage to the player, maybe for giving damage to other events too (if toucan enables friendly fire)
                StartEnemyAttack();
            }


            // キャラクターが生存している間、
            // 毎フレーム、イベント内容の実行前にこのメソッドがコールされます。
            // This method will be called every frame while the character is alive, before the event content is executed.
        }

        private void StartPlayerAttack()
        {
            SourceAttackingEvents(true);
        }

        private void StartEnemyAttack()
        {
            if (!dataInitialized)
            {
                GetFatherData();
                return;
            }

            if (!collidesWithPlayer) EnemyAttackPlayer();
            SourceAttackingEvents(false);
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

        private void EnemyAttackPlayer()
        {
            if (mapChr.collisionStatus.hitChrList.Count <= 0) return;
            try
            {
                var colliding = mapChr.collisionStatus.hitChrList.Find(x => x != null && !x.IsEvent);
                collidesWithPlayer = colliding != null;


                if (!collidesWithPlayer) return;
                //Execute toucan's formula 
                GiveDamage(mapChr.guId, null);
                GameMain.PushLog(DebugDialog.LogEntry.LogType.EVENT, "GeneratedEvent", "Player will take damage!");
            }
            catch (Exception ex)
            {
                GameMain.PushLog(DebugDialog.LogEntry.LogType.EVENT, "GeneratedEvent", "Error:" + ex.Message);
            }

        }

        private void SourceAttackingEvents(bool isPlayerAttacking)
        {
            if (mapChr.collisionStatus.hitChrList.Count <= 0) return;
            try
            {
                var colliding = mapChr.collisionStatus.hitChrList.FindAll(x => x != null && x.IsEvent && x.rom.Cast.tags.ToLower().Contains(canTakeDamageTag));

                foreach (var item in colliding)
                {
                    if (filteredColliding.Contains(item)) continue;
                    GiveDamage(isPlayerAttacking ? Guid.Empty : mapChr.guId, item);

                    filteredColliding.Add(item);
                }
            }
            catch (Exception ex)
            {
                GameMain.PushLog(DebugDialog.LogEntry.LogType.EVENT, "GeneratedEvent", "Error:" + ex.Message);
            }


        }

        private void GiveDamage(Guid guid, MapCharacter target = null)
        {
            mapScene.owner.data.system.SetVariable(strDamage, 0, target != null ? target.guId : Guid.Empty, false);
            double attack = mapScene.owner.data.system.GetVariable(strAttack, guid, guid != Guid.Empty);
            double defense = mapScene.owner.data.system.GetVariable(strDefense, target != null ? target.guId : Guid.Empty, guid != Guid.Empty);
            GameMain.PushLog(DebugDialog.LogEntry.LogType.EVENT, "GeneratedEvent", $"Source attack is: {attack} and target defense is {defense}");
            double damage = attack >= defense ? (attack * 2) - defense : attack * (attack / defense);
            double random = GetRandomNumber(0.9, 1.1);

            damage *= random;

            damage = Math.Ceiling(damage);
            mapScene.owner.data.system.SetVariable(strDamage, damage, target != null ? target.guId : Guid.Empty, false);
            double HP;

            HP = target == null ? mapScene.owner.data.party.GetPlayer(0).hitpoint : mapScene.owner.data.system.GetVariable("HP", target != null ? target.guId : Guid.Empty, false);
            HP -= damage;

            if(target == null)
            {
                mapScene.owner.data.party.GetPlayer(0).hitpoint = (int)HP;
                mapScene.owner.data.party.GetPlayer(0).consistency();

                if (mapScene.owner.data.party.isGameOver())
                {
                    mapScene.DoGameOver();
                }
            }
            mapScene.owner.data.system.SetVariable("HP", HP, target != null ? target.guId : Guid.Empty, false);
            GameMain.PushLog(DebugDialog.LogEntry.LogType.EVENT, "GeneratedEvent", $"Giving damage: {damage}");
        }

        private void GetFatherData()
        {
            if (!mapChr.IsDynamicGenerated || mapChr.collisionStatus.hitChrList.Count <= 0) return;


            var father = mapChr.collisionStatus.hitChrList[0];
            fatherName = mapChr.collisionStatus.hitChrList[0].rom.name;
            fatherGuid = mapChr.collisionStatus.hitChrList[0].guId;

            GameMain.PushLog(DebugDialog.LogEntry.LogType.EVENT, "GeneratedEvent", $"father: {fatherName}");

            double attack = mapScene.owner.data.system.GetVariable(strAttack, fatherGuid, false);

            mapScene.owner.data.system.SetVariable(strAttack, attack, mapChr.guId, true);

            if (!filteredColliding.Contains(father)) filteredColliding.Add(father);
            dataInitialized = true;
        }

        public double GetRandomNumber(double minimum, double maximum)
        {
            Random random = new Random();
            return random.NextDouble() * (maximum - minimum) + minimum;
        }
    }
}
