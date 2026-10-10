using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using StardustAtelier.Core;

namespace StardustAtelier.Meta
{
    // [실제 코드에 남길 주석] 진행 저장 형식(기획서 14.3). 플레이 도중의 보드는 저장하지 않습니다.
    [Serializable] public sealed class SaveData
    {
        public int version = 1;
        public int gold;
        // 키는 레벨 번호·캐릭터 ID를 글자로 쓴 것입니다("1", "105").
        public Dictionary<string,LevelSave> levels = new Dictionary<string,LevelSave>();
        public Dictionary<string,CharacterSave> characters = new Dictionary<string,CharacterSave>();
        // 마지막으로 쓴 파티. 빈 슬롯은 null입니다.
        public int?[] lastParty = new int?[0];
        public List<int> tutorialsSeen = new List<int>();
        // 처음 생길 때 설명을 본 아이템 종류(13.4의 첫 생성 설명). 예: "Rocket".
        public List<string> itemsSeen = new List<string>();
        public SettingsSave settings = new SettingsSave();
    }
    [Serializable] public sealed class LevelSave { public bool cleared; public int bestStars, consecutiveFails, attempts; }
    [Serializable] public sealed class CharacterSave { public int level = 1; }
    [Serializable] public sealed class SettingsSave { public bool bgm = true, sfx = true; }

    public static class SaveService
    {
        public const string FileName = "save.json", TempName = "save.tmp", BadName = "save.bad.json";

        // 파일이 없으면 새 저장(골드 0, 레벨 1만 열림)입니다.
        // 읽지 못하면 save.bad.json으로 이름을 바꾸고 새 저장으로 시작하며, recovered를 true로 돌려줍니다.
        public static SaveData Load(string directory, out bool recovered)
        {
            recovered = false;
            string path = Path.Combine(directory,FileName);
            if (!File.Exists(path)) return new SaveData();
            try
            {
                SaveData data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(path));
                if (data == null || data.version != 1) throw new InvalidDataException("Unsupported save.");
                return data;
            }
            catch (Exception)
            {
                string bad = Path.Combine(directory,BadName);
                if (File.Exists(bad)) File.Delete(bad);
                File.Move(path,bad);
                recovered = true;
                return new SaveData();
            }
        }

        // save.tmp에 먼저 쓴 뒤 교체합니다. 쓰는 도중 앱이 죽어도 이전 파일이 남습니다.
        public static void Save(string directory, SaveData data)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory,FileName), temp = Path.Combine(directory,TempName);
            File.WriteAllText(temp,JsonConvert.SerializeObject(data));
            if (File.Exists(path)) File.Replace(temp,path,null);
            else File.Move(temp,path);
        }

        // 값 보정(14.3): 골드 < 0 → 0, 캐릭터 레벨 1~최대 레벨, 보유하지 않은 ID가 lastParty에 있으면 null.
        // 지급 조건(해당 레벨 첫 클리어)을 만족하는데 빠진 캐릭터는 Lv 1로 추가합니다.
        public static void Normalize(SaveData data, LevelCatalog levels, CharacterCatalog characters)
        {
            if (data.levels == null) data.levels = new Dictionary<string,LevelSave>();
            if (data.characters == null) data.characters = new Dictionary<string,CharacterSave>();
            if (data.lastParty == null) data.lastParty = new int?[0];
            if (data.tutorialsSeen == null) data.tutorialsSeen = new List<int>();
            if (data.itemsSeen == null) data.itemsSeen = new List<string>();
            if (data.settings == null) data.settings = new SettingsSave();
            if (data.gold < 0) data.gold = 0;
            foreach (LevelSave level in data.levels.Values)
            {
                level.bestStars = Math.Max(0,Math.Min(3,level.bestStars));
                if (level.consecutiveFails < 0) level.consecutiveFails = 0;
                if (level.attempts < 0) level.attempts = 0;
            }
            foreach (LevelData level in levels.levels)
            {
                LevelSave saved;
                if (level.onFirstClear == null || level.onFirstClear.grantCharacter == 0) continue;
                if (!data.levels.TryGetValue(level.id.ToString(),out saved) || !saved.cleared) continue;
                string id = level.onFirstClear.grantCharacter.ToString();
                if (!data.characters.ContainsKey(id)) data.characters[id] = new CharacterSave();
            }
            int maxLevel = characters.growth.maxLevel;
            foreach (KeyValuePair<string,CharacterSave> entry in data.characters)
            {
                if (entry.Value == null) continue;
                entry.Value.level = Math.Max(1,Math.Min(maxLevel,entry.Value.level));
            }
            // null로 저장된 캐릭터 항목은 Lv 1로 되살립니다.
            foreach (string key in new List<string>(data.characters.Keys))
                if (data.characters[key] == null) data.characters[key] = new CharacterSave();
            for (int i = 0; i < data.lastParty.Length; i++)
                if (data.lastParty[i].HasValue && !data.characters.ContainsKey(data.lastParty[i].Value.ToString())) data.lastParty[i] = null;
        }
        public static LevelSave Level(SaveData data, int id)
        {
            LevelSave level;
            if (!data.levels.TryGetValue(id.ToString(),out level) || level == null) { level = new LevelSave(); data.levels[id.ToString()] = level; }
            return level;
        }
    }
}
