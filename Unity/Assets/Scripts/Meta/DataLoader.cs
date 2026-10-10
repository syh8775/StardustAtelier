using System;
using System.IO;
using Newtonsoft.Json;
using StardustAtelier.Core;

namespace StardustAtelier.Meta
{
    public static class DataLoader
    {
        // [실제 코드에 남길 주석] 문서의 중첩 좌표 배열을 손실 없이 읽기 위해 JSON.NET을 사용합니다.
        public static LevelCatalog LoadLevels(string directory)
        {
            LevelCatalog catalog=Read<LevelCatalog>(directory,"levels.json");
            if (catalog.version!=1 || catalog.levels==null) throw new InvalidDataException("Unsupported level catalog.");
            return catalog;
        }
        public static CharacterCatalog LoadCharacters(string directory)
        {
            CharacterCatalog catalog=Read<CharacterCatalog>(directory,"characters.json");
            if (catalog.version!=1 || catalog.characters==null || catalog.skills==null || catalog.growth==null) throw new InvalidDataException("Unsupported character catalog.");
            return catalog;
        }
        public static GameConfig LoadConfig(string directory)
        {
            GameConfig config=Read<GameConfig>(directory,"config.json");
            if (config.version!=1 || config.rules==null || config.input==null || config.timing==null || config.rewards==null || config.layout==null)
                throw new InvalidDataException("Unsupported config.");
            if (config.rules.generateMaxTries<=0 || config.rules.maxStepsPerResolution<=0 ||
                config.layout.referenceWidth<=0 || config.layout.referenceHeight<=0 || config.layout.boardAreaPx<=0)
                throw new InvalidDataException("Invalid config limits.");
            return config;
        }
        private static T Read<T>(string directory, string filename)
        {
            T value=JsonConvert.DeserializeObject<T>(File.ReadAllText(Path.Combine(directory,filename)));
            if (value==null) throw new InvalidDataException("Empty data: "+filename);
            return value;
        }
    }
}
