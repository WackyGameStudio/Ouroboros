using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    public sealed class OSBombView : MonoBehaviour
    {
        private const int CircleSegments = 64;
        private const float ExplosionFlashDuration = 0.3f;

        [SerializeField] private LineRenderer ringLine;
        [SerializeField] private SpriteRenderer explosionFill;

        private Vector2 _start;
        private Vector2 _forward = Vector2.right;
        private float _radius;
        private OSBombTurnSide _turnSide;
        private float _flashRemaining;

        public bool IsRingVisible => ringLine != null && ringLine.enabled;

        private void OnDisable()
        {
            ResetVisual();
        }

        private void Update()
        {
            if (_flashRemaining <= 0f || explosionFill == null)
            {
                return;
            }

            _flashRemaining = Mathf.Max(0f, _flashRemaining - Time.unscaledDeltaTime);
            var color = explosionFill.color;
            color.a = 0.34f * (_flashRemaining / ExplosionFlashDuration);
            explosionFill.color = color;
            if (_flashRemaining <= 0f)
            {
                explosionFill.enabled = false;
            }
        }

        public void Configure(LineRenderer line, SpriteRenderer fill)
        {
            ringLine = line;
            explosionFill = fill;
            ResetVisual();
        }

        public void BeginDrawing(
            Vector2 start,
            Vector2 forward,
            float radius,
            OSBombTurnSide turnSide)
        {
            _start = start;
            _forward = forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector2.right;
            _radius = Mathf.Max(0f, radius);
            _turnSide = turnSide;
            _flashRemaining = 0f;
            if (explosionFill != null)
            {
                explosionFill.enabled = false;
            }

            if (ringLine != null)
            {
                ringLine.enabled = true;
                ringLine.loop = false;
            }

            SetDrawingProgress(0f);
        }

        public void ShowPreview(
            Vector2 start,
            Vector2 forward,
            float radius,
            OSBombTurnSide turnSide)
        {
            BeginDrawing(start, forward, radius, turnSide);
            SetDrawingProgress(1f);
        }

        public void HidePreview()
        {
            if (_flashRemaining <= 0f)
            {
                Complete();
            }
        }

        public void SetDrawingProgress(float progress)
        {
            if (ringLine == null || !ringLine.enabled)
            {
                return;
            }

            var clamped = Mathf.Clamp01(progress);
            var completedSegments = Mathf.Clamp(
                Mathf.FloorToInt(clamped * CircleSegments),
                0,
                CircleSegments);
            var pointCount = Mathf.Clamp(completedSegments + 2, 2, CircleSegments + 1);
            ringLine.positionCount = pointCount;
            for (var index = 0; index < pointCount; index++)
            {
                var pointProgress = index == pointCount - 1
                    ? clamped
                    : index / (float)CircleSegments;
                ringLine.SetPosition(
                    index,
                    OSBombMath.CalculateOrbitPoint(
                        _start,
                        _forward,
                        _radius,
                        pointProgress,
                        _turnSide));
            }
        }

        public void Explode(Vector2 center, float radius)
        {
            SetDrawingProgress(1f);
            if (explosionFill == null)
            {
                return;
            }

            explosionFill.transform.position = new Vector3(center.x, center.y, 0f);
            var spriteSize = explosionFill.sprite != null
                ? explosionFill.sprite.bounds.size
                : Vector3.one;
            var diameter = Mathf.Max(0.01f, radius * 2f);
            explosionFill.transform.localScale = new Vector3(
                diameter / Mathf.Max(0.01f, spriteSize.x),
                diameter / Mathf.Max(0.01f, spriteSize.y),
                1f);
            explosionFill.color = new Color(0.32f, 0.92f, 1f, 0.34f);
            explosionFill.enabled = true;
            _flashRemaining = ExplosionFlashDuration;
        }

        public void Complete()
        {
            if (ringLine != null)
            {
                ringLine.enabled = false;
                ringLine.positionCount = 0;
            }
        }

        public void Cancel()
        {
            ResetVisual();
        }

        private void ResetVisual()
        {
            _flashRemaining = 0f;
            if (ringLine != null)
            {
                ringLine.enabled = false;
                ringLine.positionCount = 0;
            }

            if (explosionFill != null)
            {
                explosionFill.enabled = false;
            }
        }
    }
}
