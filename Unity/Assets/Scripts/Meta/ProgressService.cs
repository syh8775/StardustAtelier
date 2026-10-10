using System;
using StardustAtelier.Core;

namespace StardustAtelier.Meta
{
    // [실제 코드에 남길 주석] 클리어 보상 계산(기획서 10.4). 수치는 config.json의 rewards에서 읽습니다.
    public static class ProgressService
    {
        // previousBest: 이번 클리어 전의 최고 별. 0이면 첫 클리어입니다.
        // 첫 클리어: 100 + 50 × (별 − 1). 다시 클리어: 20 + 50 × max(0, 별 − 기존 최고 별). 실패는 보상이 없습니다.
        public static int ClearGold(RewardConfig rewards, int stars, int previousBest)
        {
            if (stars <= 0) return 0;
            if (previousBest <= 0) return rewards.firstClearBase+rewards.firstClearPerExtraStar*(stars-1);
            return rewards.replayBase+rewards.replayPerNewStar*Math.Max(0,stars-previousBest);
        }
    }
}
