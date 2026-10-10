using System;
using System.Collections.Generic;

namespace StardustAtelier.Core
{
    public static class LevelValidator
    {
        // [실제 코드에 남길 주석] 오류가 있는 레벨은 호출자가 플레이 목록에서 제외합니다.
        public static List<string> Validate(LevelData level, CharacterCatalog characters, RuleConfig rules)
        {
            List<string> errors = new List<string>();
            if (level == null) { errors.Add("Level is null."); return errors; }
            if (level.width < 5 || level.width > 9 || level.height < 5 || level.height > 9) errors.Add("Board size must be 5..9.");
            if (level.moves <= 0) errors.Add("Moves must be positive.");
            if (level.layers == null || level.features == null) { errors.Add("Layers/features are missing."); return errors; }
            CheckLayer(level.layers.mask,level,"mask",errors);
            CheckLayer(level.layers.ice,level,"ice",errors);
            CheckLayer(level.layers.occupant,level,"occupant",errors);
            CheckLayer(level.layers.chain,level,"chain",errors);
            if (level.colors == null || level.colors.Length < 3 || level.colors.Length > 5) errors.Add("Colors must contain 3..5 entries.");
            else
            {
                HashSet<string> codes = new HashSet<string>();
                foreach (string color in level.colors)
                    if (color == null || color.Length != 1 || "RBGYP".IndexOf(color[0]) < 0 || !codes.Add(color)) errors.Add("Invalid/duplicate color.");
            }
            if (level.goals == null || level.goals.Length < 1 || level.goals.Length > 3) errors.Add("Goals must contain 1..3 entries.");
            if (errors.Count > 0) return errors;
            int ice = 0, box = 0, vine = 0, chain = 0, relic = 0;
            for (int r = 0; r < level.height; r++)
                for (int c = 0; c < level.width; c++)
                {
                    char m=level.layers.mask[r][c], i=level.layers.ice[r][c], o=level.layers.occupant[r][c], l=level.layers.chain[r][c];
                    if (".#".IndexOf(m)<0 || "012".IndexOf(i)<0 || "?RBGYPXVO-".IndexOf(o)<0 || ".C".IndexOf(l)<0) errors.Add("Unknown layer token at " + new CellPosition(r,c));
                    if (m=='#' && (i!='0' || o!='-' || l!='.')) errors.Add("Hole contains data.");
                    if ((o=='X' || o=='V') && i!='0') errors.Add("Fixed obstacle is on ice.");
                    if (l=='C' && "?RBGYP".IndexOf(o)<0) errors.Add("Chain requires a color block.");
                    if ("RBGYP".IndexOf(o)>=0 && Array.IndexOf(level.colors,o.ToString())<0) errors.Add("Fixed color is outside palette.");
                    if (i>'0') ice++;
                    if (o=='X') box++; if (o=='V') vine++; if (o=='O') relic++; if (l=='C') chain++;
                }
            foreach (GoalData goal in level.goals)
            {
                if (goal == null || goal.count<=0) { errors.Add("Goal count must be positive."); continue; }
                int available = int.MaxValue;
                switch (goal.type)
                {
                    case "Collect": if (Array.IndexOf(level.colors,goal.color)<0) errors.Add("Collection color is outside palette."); break;
                    case "Ice": available=ice; break;
                    case "Box": available=box; break;
                    case "Vine": available=vine; break;
                    case "Chain": available=chain; break;
                    case "Relic": available=relic; break;
                    default: errors.Add("Unknown goal type: "+goal.type); break;
                }
                if (goal.count>available) errors.Add("Goal exceeds starting count: "+goal.type);
            }
            for (int c=0;c<level.width;c++)
            {
                int bottom=level.height-1;
                while (bottom>=0 && level.layers.mask[bottom][c]=='#') bottom--;
                if (bottom>=0 && level.layers.occupant[bottom][c]=='O') errors.Add("Relic starts on collection cell.");
            }
            int owned=0;
            if (characters == null || characters.characters == null) errors.Add("Character catalog is missing.");
            else
            {
                foreach (CharacterData character in characters.characters) if (character.unlockLevel<=level.id) owned++;
                // 레벨 2부터 3칸을 보여주고 빈 칸을 허용합니다. 슬롯이 있으면 보유 캐릭터가 1명 이상이어야 합니다.
                if (level.partySlots<0 || level.partySlots>3 || (level.partySlots>0 && owned<1)) errors.Add("Party slots need at least one available character.");
                if (level.recommended != null)
                    foreach (string color in level.recommended)
                    {
                        bool available=false;
                        foreach (CharacterData character in characters.characters)
                            if (character.unlockLevel<=level.id && character.attr==color) available=true;
                        if (!available) errors.Add("Recommended attribute is locked: "+color);
                    }
            }
            if (errors.Count>0) return errors;
            // 명세 14.4에 지정된 30개 시드로 시작 조건과 비상 처리 부재를 검사합니다.
            for (ulong seed=1;seed<=30;seed++)
            {
                Pcg32 generation=new Pcg32(seed,1), shuffle=new Pcg32(seed,4);
                try
                {
                    GeneratedBoard generated=BoardGenerator.Generate(level,rules,ref generation,ref shuffle);
                    if (generated.emergency || !BoardGenerator.IsStableStart(generated.board,level.features.squareMatch))
                        errors.Add("Start condition failed for seed "+seed);
                    if (level.tutorial != null && level.tutorial.type=="Guided")
                    {
                        int[][] swap=level.tutorial.swap;
                        if (swap==null || swap.Length!=2 || swap[0]==null || swap[1]==null || swap[0].Length!=2 || swap[1].Length!=2 ||
                            !MoveFinder.IsValid(generated.board,new CellPosition(swap[0][0],swap[0][1]),new CellPosition(swap[1][0],swap[1][1]),level.features.squareMatch))
                            errors.Add("Invalid guided swap for seed "+seed);
                    }
                }
                catch (Exception ex) { errors.Add("Generation failed: "+ex.Message); break; }
            }
            return errors;
        }
        private static void CheckLayer(string[] rows, LevelData level, string name, List<string> errors)
        {
            if (rows==null || rows.Length!=level.height) { errors.Add("Invalid row count: "+name); return; }
            foreach (string row in rows) if (row==null || row.Length!=level.width) { errors.Add("Invalid row width: "+name); return; }
        }
    }
}
