using System;

namespace Ouroboros.Core
{
    public enum OSTutorialStage
    {
        None,
        Movement,
        AutoAttack,
        WaitForFragment,
        BodyGrowth,
        WaitForBodyFour,
        Blast,
        WaitForCut,
        CutDifference,
        Completed
    }

    /// <summary>
    /// Owns the first-session-only tutorial sequence without depending on Unity UI or time scale.
    /// </summary>
    public sealed class OSTutorialProgress
    {
        private const float RequiredMovementSeconds = 1f;
        private const float BlastTimeoutSeconds = 20f;
        private const float CutMessageSeconds = 3f;

        private float _stageElapsed;
        private float _movementElapsed;
        private bool _fragmentSeen;
        private bool _roleConfirmed;
        private bool _explosionSeen;
        private bool _cutSeen;
        private int _largestBodyCount;

        public OSTutorialStage Stage { get; private set; }
        public bool HasStarted { get; private set; }
        public bool IsVisible => Stage is OSTutorialStage.Movement or
            OSTutorialStage.AutoAttack or OSTutorialStage.BodyGrowth or
            OSTutorialStage.Blast or OSTutorialStage.CutDifference;
        public float StageElapsed => _stageElapsed;
        public float MovementElapsed => _movementElapsed;

        public bool BeginFirstSession()
        {
            if (HasStarted)
            {
                return false;
            }

            HasStarted = true;
            SetStage(OSTutorialStage.Movement);
            return true;
        }

        public void Advance(float unscaledDeltaTime, bool isMoving)
        {
            if (!HasStarted || !float.IsFinite(unscaledDeltaTime) || unscaledDeltaTime <= 0f)
            {
                return;
            }

            _stageElapsed += unscaledDeltaTime;
            if (Stage == OSTutorialStage.Movement)
            {
                if (isMoving)
                {
                    _movementElapsed += unscaledDeltaTime;
                }

                if (_movementElapsed >= RequiredMovementSeconds)
                {
                    SetStage(OSTutorialStage.AutoAttack);
                }
            }
            else if (Stage == OSTutorialStage.Blast &&
                     (_explosionSeen || _stageElapsed >= BlastTimeoutSeconds))
            {
                EnterWaitForCut();
            }
            else if (Stage == OSTutorialStage.CutDifference &&
                     _stageElapsed >= CutMessageSeconds)
            {
                SetStage(OSTutorialStage.Completed);
            }
        }

        public void NotifyEnemyDefeated()
        {
            if (!HasStarted || Stage != OSTutorialStage.AutoAttack)
            {
                return;
            }

            SetStage(_fragmentSeen ? OSTutorialStage.BodyGrowth : OSTutorialStage.WaitForFragment);
            ResolveBodyGrowthProgress();
        }

        public void NotifyFragmentCollected()
        {
            if (!HasStarted)
            {
                return;
            }

            _fragmentSeen = true;
            if (Stage == OSTutorialStage.WaitForFragment)
            {
                SetStage(OSTutorialStage.BodyGrowth);
                ResolveBodyGrowthProgress();
            }
        }

        public void NotifyRoleConfirmed()
        {
            if (!HasStarted)
            {
                return;
            }

            _roleConfirmed = true;
            ResolveBodyGrowthProgress();
        }

        public void NotifyBodyCount(int count)
        {
            if (!HasStarted)
            {
                return;
            }

            _largestBodyCount = Math.Max(_largestBodyCount, Math.Max(0, count));
            ResolveBodyGrowthProgress();
        }

        public void NotifyExplosionResolved()
        {
            if (!HasStarted)
            {
                return;
            }

            _explosionSeen = true;
            if (Stage == OSTutorialStage.Blast)
            {
                EnterWaitForCut();
            }
        }

        public void NotifyBodyCut()
        {
            if (!HasStarted)
            {
                return;
            }

            _cutSeen = true;
            if (Stage == OSTutorialStage.WaitForCut)
            {
                SetStage(OSTutorialStage.CutDifference);
            }
        }

        private void ResolveBodyGrowthProgress()
        {
            if (Stage != OSTutorialStage.BodyGrowth && Stage != OSTutorialStage.WaitForBodyFour)
            {
                return;
            }

            if (!_roleConfirmed)
            {
                return;
            }

            if (_largestBodyCount >= 4)
            {
                SetStage(OSTutorialStage.Blast);
            }
            else if (Stage == OSTutorialStage.BodyGrowth)
            {
                SetStage(OSTutorialStage.WaitForBodyFour);
            }
        }

        private void EnterWaitForCut()
        {
            SetStage(_cutSeen ? OSTutorialStage.CutDifference : OSTutorialStage.WaitForCut);
        }

        private void SetStage(OSTutorialStage stage)
        {
            Stage = stage;
            _stageElapsed = 0f;
        }
    }
}
