using System;
using System.IO;
using NUnit.Framework;
using StardustAtelier.Core;
using StardustAtelier.Meta;

namespace StardustAtelier.Tests
{
    // [실제 코드에 남길 주석] 진행 저장(기획서 14.3)을 임시 폴더에서 검사합니다. 실제 저장 폴더는 건드리지 않습니다.
    public sealed class SaveTests
    {
        private string directory;
        private static string Data { get { return Path.Combine(UnityEngine.Application.streamingAssetsPath,"Data"); } }
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(),"stardust-save-test-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory,true); }

        [Test] public void MissingFileStartsNewSave()
        {
            bool recovered;
            SaveData data = SaveService.Load(directory,out recovered);
            Assert.That(recovered,Is.False); Assert.That(data.gold,Is.Zero);
            Assert.That(data.levels,Is.Empty); Assert.That(data.characters,Is.Empty);
            Assert.That(data.settings.bgm,Is.True); Assert.That(data.settings.sfx,Is.True);
        }
        [Test] public void SaveAndLoadRoundTripsAndLeavesNoTempFile()
        {
            SaveData data = new SaveData { gold = 350 };
            LevelSave level = SaveService.Level(data,1); level.cleared = true; level.bestStars = 3; level.attempts = 2;
            data.characters["105"] = new CharacterSave { level = 3 };
            data.lastParty = new int?[] { 105,null,null };
            data.tutorialsSeen.Add(1); data.itemsSeen.Add("Rocket");
            SaveService.Save(directory,data);
            SaveService.Save(directory,data); // 두 번째 쓰기는 기존 파일을 교체합니다.
            Assert.That(File.Exists(Path.Combine(directory,SaveService.FileName)),Is.True);
            Assert.That(File.Exists(Path.Combine(directory,SaveService.TempName)),Is.False);
            bool recovered;
            SaveData loaded = SaveService.Load(directory,out recovered);
            Assert.That(recovered,Is.False); Assert.That(loaded.gold,Is.EqualTo(350));
            Assert.That(loaded.levels["1"].cleared,Is.True); Assert.That(loaded.levels["1"].bestStars,Is.EqualTo(3));
            Assert.That(loaded.levels["1"].attempts,Is.EqualTo(2));
            Assert.That(loaded.characters["105"].level,Is.EqualTo(3));
            Assert.That(loaded.lastParty,Is.EqualTo(new int?[] { 105,null,null }));
            Assert.That(loaded.tutorialsSeen,Is.EqualTo(new[] { 1 })); Assert.That(loaded.itemsSeen,Is.EqualTo(new[] { "Rocket" }));
        }
        [Test] public void BrokenFileIsRenamedAndNewSaveStarts()
        {
            // E42: save.json이 깨지면 save.bad.json으로 바꾸고 새로 시작합니다.
            File.WriteAllText(Path.Combine(directory,SaveService.FileName),"{ this is not json");
            bool recovered;
            SaveData data = SaveService.Load(directory,out recovered);
            Assert.That(recovered,Is.True); Assert.That(data.levels,Is.Empty);
            Assert.That(File.Exists(Path.Combine(directory,SaveService.BadName)),Is.True);
            Assert.That(File.Exists(Path.Combine(directory,SaveService.FileName)),Is.False);
        }
        [Test] public void NormalizeClampsValuesAndRestoresGrantedCharacters()
        {
            LevelCatalog levels = DataLoader.LoadLevels(Data); CharacterCatalog characters = DataLoader.LoadCharacters(Data);
            SaveData data = new SaveData { gold = -5 };
            SaveService.Level(data,1).cleared = true;             // 레벨 1 클리어 → 세이지(105)가 있어야 합니다.
            data.characters["106"] = new CharacterSave { level = 99 };
            data.lastParty = new int?[] { 105,109,null };          // 109(모르)는 보유하지 않았습니다.
            SaveService.Normalize(data,levels,characters);
            Assert.That(data.gold,Is.Zero);
            Assert.That(data.characters.ContainsKey("105"),Is.True); Assert.That(data.characters["105"].level,Is.EqualTo(1));
            Assert.That(data.characters["106"].level,Is.EqualTo(characters.growth.maxLevel));
            Assert.That(data.lastParty,Is.EqualTo(new int?[] { 105,null,null }));
        }
    }
}
