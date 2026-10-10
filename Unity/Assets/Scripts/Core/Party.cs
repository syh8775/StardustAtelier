using System;
using System.Collections.Generic;

namespace StardustAtelier.Core
{
    // [실제 코드에 남길 주석] 파티원 한 명(기획서 12장). 게이지는 정수 단위로 계산합니다(블록 1개 = 12단위).
    public sealed class PartyMember
    {
        public CharacterData character;
        public SkillData skill;
        public int level = 1;
        public float costReductionPerLevel;

        // 필요 게이지 = max(1, 반올림(기본 × (1 − 0.015 × (Lv − 1)))) × 12. 반올림은 Math.Floor(x + 0.5).
        public int RequiredUnits(int unitsPerBlock)
        {
            double cost = character.baseCost*(1-costReductionPerLevel*(level-1));
            return Math.Max(1,(int)Math.Floor(cost+0.5))*unitsPerBlock;
        }
    }

    // 스킬을 쓸 수 있는지(12.3)와 못 쓰는 이유. View는 이 값으로 초상화 빛남과 안내 문구를 정합니다.
    public enum SkillState { Empty, Charging, Locked, NoTarget, Ready }

    // 스킬 대상 고르기(12.5). 보드를 바꾸지 않는 조회이며, 난수는 호출하는 쪽이 넘긴 스킬 난수만 씁니다.
    public static class SkillTargets
    {
        public static List<CellPosition> Pick(Board board, SkillData skill, ref Pcg32 random)
        {
            List<CellPosition> result = new List<CellPosition>();
            switch (skill.type)
            {
                case "HitObstaclesThenBlocks":
                {
                    // 장애물 칸(상자·덩굴·사슬·얼음)에서 최대 count개, 모자라면 보통 블록으로 채웁니다.
                    List<CellPosition> obstacles = new List<CellPosition>(), blocks = new List<CellPosition>();
                    foreach (CellPosition p in board.Positions())
                    {
                        Cell cell = board[p];
                        if (!cell.exists) continue;
                        bool obstacle = cell.ice > 0 || cell.chained || cell.occupant.kind == OccupantKind.Box || cell.occupant.kind == OccupantKind.Vine;
                        if (obstacle) obstacles.Add(p);
                        else if (IsNormalBlock(cell)) blocks.Add(p);
                    }
                    result.AddRange(TakeRandom(obstacles,skill.count,ref random));
                    result.AddRange(TakeRandom(blocks,skill.count-result.Count,ref random));
                    break;
                }
                case "HitBottomRow":
                {
                    // 유효 칸이 있는 가장 아래 행의 모든 유효 칸.
                    for (int r = board.Height-1; r >= 0 && result.Count == 0; r--)
                        for (int c = 0; c < board.Width; c++) if (board[r,c].exists) result.Add(new CellPosition(r,c));
                    break;
                }
                case "ConvertToSpecial":
                {
                    List<CellPosition> blocks = new List<CellPosition>();
                    foreach (CellPosition p in board.Positions()) if (IsNormalBlock(board[p])) blocks.Add(p);
                    result.AddRange(TakeRandom(blocks,skill.count,ref random));
                    break;
                }
                // AddMoves는 보드 대상이 없습니다.
            }
            result.Sort(MatchFinder.ReadingOrder);
            return result;
        }
        // "보통 블록" = 사슬 없는 색 블록.
        public static bool IsNormalBlock(Cell cell)
        { return cell.exists && !cell.chained && cell.occupant.kind == OccupantKind.ColorBlock; }
        // 대상이 필요 없는 스킬(모르 · 이동 +1)은 항상 쓸 수 있습니다.
        public static bool NeedsTarget(SkillData skill) { return skill.type != "AddMoves"; }

        // 후보를 읽기 순서로 받은 뒤 앞에서부터 k번 Fisher–Yates로 중복 없이 뽑습니다(15.1).
        private static List<CellPosition> TakeRandom(List<CellPosition> candidates, int count, ref Pcg32 random)
        {
            List<CellPosition> list = new List<CellPosition>(candidates);
            int k = Math.Max(0,Math.Min(count,list.Count));
            for (int i = 0; i < k; i++)
            {
                int j = i+random.NextInt(list.Count-i);
                CellPosition temp = list[i]; list[i] = list[j]; list[j] = temp;
            }
            return list.GetRange(0,k);
        }
    }
}
