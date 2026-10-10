using System;
using System.Collections.Generic;

namespace StardustAtelier.Core
{
    public enum BlockColor { None = -1, R, B, G, Y, P }
    public enum OccupantKind { Empty, ColorBlock, Rocket, Bomb, Meteor, Prism, Box, Vine, Relic }
    public enum Orientation { H, V }

    [Serializable]
    public struct CellPosition : IEquatable<CellPosition>
    {
        public int r, c;
        public CellPosition(int row, int column) { r = row; c = column; }
        public bool Equals(CellPosition other) { return r == other.r && c == other.c; }
        public override bool Equals(object value) { return value is CellPosition && Equals((CellPosition)value); }
        public override int GetHashCode() { return unchecked(r * 397 ^ c); }
        public override string ToString() { return "(" + r + ", " + c + ")"; }
    }

    // [실제 코드에 남길 주석] 바닥·덮개는 칸에 남고, 점유물만 스왑하거나 낙하합니다.
    public struct Occupant
    {
        public OccupantKind kind;
        public BlockColor color;
        public Orientation orientation;
        public int hp;
        public bool IsSpecial { get { return kind >= OccupantKind.Rocket && kind <= OccupantKind.Prism; } }
        // 로켓·폭탄·별똥별·프리즘은 색(속성)이 없는 아이템입니다. 색은 색 블록만 가집니다.
        public bool IsColored { get { return kind == OccupantKind.ColorBlock; } }
        public static Occupant Empty { get { return new Occupant { color = BlockColor.None }; } }
        public static Occupant Block(BlockColor color) { return new Occupant { kind = OccupantKind.ColorBlock, color = color }; }
    }

    public sealed class Cell
    {
        public bool exists = true;
        public int ice;
        public bool chained;
        public Occupant occupant = Occupant.Empty;
        public bool CanSwap { get { return exists && !chained && (occupant.IsColored || occupant.IsSpecial); } }
        public bool CanMatch { get { return exists && occupant.IsColored; } }
        public bool IsFixed { get { return exists && (chained || occupant.kind == OccupantKind.Box || occupant.kind == OccupantKind.Vine); } }
        public Cell Clone() { return new Cell { exists = exists, ice = ice, chained = chained, occupant = occupant }; }
    }

    public sealed class Board
    {
        private readonly Cell[,] cells;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public Board(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("width");
            Width = width; Height = height; cells = new Cell[height, width];
            for (int r = 0; r < height; r++)
                for (int c = 0; c < width; c++) cells[r, c] = new Cell();
        }
        public Cell this[int r, int c] { get { return cells[r, c]; } }
        public Cell this[CellPosition p] { get { return cells[p.r, p.c]; } }
        public bool Contains(CellPosition p) { return p.r >= 0 && p.r < Height && p.c >= 0 && p.c < Width; }
        public IEnumerable<CellPosition> Positions()
        {
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++) yield return new CellPosition(r, c);
        }
        public void Swap(CellPosition a, CellPosition b)
        {
            Occupant value = this[a].occupant; this[a].occupant = this[b].occupant; this[b].occupant = value;
        }
        public Board Clone()
        {
            Board copy = new Board(Width, Height);
            foreach (CellPosition p in Positions()) copy.cells[p.r, p.c] = this[p].Clone();
            return copy;
        }
    }

    [Serializable] public sealed class LevelCatalog { public int version; public LevelData[] levels; }
    [Serializable] public sealed class LevelData
    {
        public int id, width, height, moves, partySlots;
        public string[] colors, recommended;
        public GoalData[] goals;
        public LayerData layers;
        public FeatureData features;
        public bool hard;
        public float targetClear;
        public TutorialData tutorial;
        public int[] startGauge;
        public FirstClearData onFirstClear;
    }
    [Serializable] public sealed class GoalData { public string type, color; public int count; }
    [Serializable] public sealed class LayerData { public string[] mask, ice, occupant, chain; }
    [Serializable] public sealed class FeatureData { public bool squareMatch; }
    [Serializable] public sealed class TutorialData { public string type, textKey; public int[][] swap; public int charId; }
    [Serializable] public sealed class FirstClearData { public int grantCharacter; }
    [Serializable] public sealed class GameConfig
    {
        public int version;
        public InputConfig input;
        public TimingConfig timing;
        public RuleConfig rules;
        public RewardConfig rewards;
        public LayoutConfig layout;
    }
    [Serializable] public sealed class InputConfig { public float dragThresholdCellRatio, shakeSec; }
    [Serializable] public sealed class TimingConfig
    {
        public float introBannerSec, swapSec, swapBackSec, clearSec, rocketSec, staggerPerDistanceSec,
            bombSec, meteorFlightSec, prismSec, comboIntroSec, fallPerCellSec, fallMinSec,
            landBounceSec, stepGapSec, shuffleSec, vineSpreadSec, hintDelaySec,
            clearBannerSec, starIntervalSec, toastSec;
    }
    [Serializable] public sealed class RuleConfig
    {
        public int maxStepsPerResolution, generateMaxTries, shuffleMaxTries, regenerateMaxTries,
            assistFailThreshold, gaugeUnitsPerBlock;
        public float assistRefillRate, star2MovesRatio, star3MovesRatio;
    }
    [Serializable] public sealed class RewardConfig { public int firstClearBase, firstClearPerExtraStar, replayBase, replayPerNewStar; }
    [Serializable] public sealed class LayoutConfig { public int referenceWidth, referenceHeight, boardAreaPx, lowMovesWarning; }
}

namespace StardustAtelier.Core
{
    [System.Serializable] public sealed class CharacterCatalog { public int version; public CharacterData[] characters; public SkillData[] skills; public GrowthData growth; }
    [System.Serializable] public sealed class CharacterData { public int id, grade, skillId, baseCost, unlockLevel; public string name, attr; }
    [System.Serializable] public sealed class SkillData { public int id, count; public string name, type, special, desc; }
    [System.Serializable] public sealed class GrowthData { public int maxLevel, goldCostPerLevel; public float costReductionPerLevel; }
}
