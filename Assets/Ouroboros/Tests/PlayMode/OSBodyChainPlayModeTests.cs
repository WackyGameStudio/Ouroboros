using System.Collections;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSBodyChainPlayModeTests
    {
        private GameObject _root;
        private Transform _head;
        private OSBodyChain _chain;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("BodyChainTestRoot");
            _head = new GameObject("Head").transform;
            _head.SetParent(_root.transform, false);
            var poolRoot = new GameObject("SegmentPool").transform;
            poolRoot.SetParent(_root.transform, false);

            var templateObject = new GameObject(
                "SegmentTemplate",
                typeof(SpriteRenderer),
                typeof(CircleCollider2D),
                typeof(OSBodySegmentView));
            templateObject.transform.SetParent(_root.transform, false);
            templateObject.GetComponent<CircleCollider2D>().isTrigger = true;
            var template = templateObject.GetComponent<OSBodySegmentView>();
            templateObject.SetActive(false);

            var chainObject = new GameObject("BodyChain");
            chainObject.transform.SetParent(_root.transform, false);
            _chain = chainObject.AddComponent<OSBodyChain>();
            _chain.ConfigureForTesting(_head, template, poolRoot);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StraightPath_PreservesSpacingOrderAndStableIdsAtAllDebugCounts()
        {
            foreach (var count in new[] { 2, 20, 40, 64 })
            {
                var result = _chain.SetDebugSegmentCount(count);
                Assert.That(result.IsAccepted, Is.True);
                Assert.That(_chain.ActiveCount, Is.EqualTo(count));
                Assert.That(_chain.PoolCapacity, Is.EqualTo(64));

                for (var step = 1; step <= 400; step++)
                {
                    _chain.SimulatePathStep(new Vector2(step * 0.1f, 0f));
                }

                const float headX = 40f;
                for (var index = 0; index < count; index++)
                {
                    var segment = _chain.GetActiveSegment(index);
                    Assert.That(segment.StableId, Is.EqualTo(index + 1));
                    Assert.That(segment.ChainIndex, Is.EqualTo(index));
                    Assert.That(segment.IsActive, Is.True);
                    Assert.That(
                        segment.View.transform.position.x,
                        Is.EqualTo(headX - ((index + 1) * 0.55f)).Within(0.001f));
                    Assert.That(segment.View.transform.position.y, Is.EqualTo(0f).Within(0.001f));
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator CircularAndZigzagPaths_RemainFiniteOrderedAndDoNotTeleport()
        {
            Assert.That(_chain.SetDebugSegmentCount(64).IsAccepted, Is.True);
            var previous = new Vector2[64];
            for (var index = 0; index < previous.Length; index++)
            {
                previous[index] = _chain.GetActiveSegment(index).View.transform.position;
            }

            var previousHead = Vector2.zero;
            for (var step = 1; step <= 600; step++)
            {
                Vector2 nextHead;
                if (step <= 300)
                {
                    var angle = step * 0.0125f;
                    nextHead = new Vector2(
                        5f * Mathf.Sin(angle),
                        5f * (1f - Mathf.Cos(angle)));
                }
                else
                {
                    var x = (step - 300) * 0.045f;
                    nextHead = new Vector2(x - 2.858f, 9.103f + (Mathf.Sin(x * 1.2f) * 1.8f));
                }

                var headStep = Vector2.Distance(previousHead, nextHead);
                _chain.SimulatePathStep(nextHead);
                for (var index = 0; index < previous.Length; index++)
                {
                    var segment = _chain.GetActiveSegment(index);
                    var position = (Vector2)segment.View.transform.position;
                    Assert.That(float.IsFinite(position.x) && float.IsFinite(position.y), Is.True);
                    Assert.That(
                        Vector2.Distance(previous[index], position),
                        Is.LessThanOrEqualTo((headStep * 1.75f) + 0.03f),
                        $"Segment {index} teleported at step {step}.");
                    Assert.That(segment.ChainIndex, Is.EqualTo(index));
                    previous[index] = position;
                }

                previousHead = nextHead;
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator StopAndRestart_PreservesSpacingWithoutChainContraction()
        {
            Assert.That(_chain.SetDebugSegmentCount(20).IsAccepted, Is.True);
            for (var step = 1; step <= 120; step++)
            {
                _chain.SimulatePathStep(new Vector2(step * 0.05f, Mathf.Sin(step * 0.04f)));
            }

            var stoppedHead = new Vector2(6f, Mathf.Sin(4.8f));
            var stoppedPositions = new Vector2[20];
            for (var index = 0; index < stoppedPositions.Length; index++)
            {
                stoppedPositions[index] = _chain.GetActiveSegment(index).View.transform.position;
            }

            for (var step = 0; step < 100; step++)
            {
                _chain.SimulatePathStep(stoppedHead);
            }

            for (var index = 0; index < stoppedPositions.Length; index++)
            {
                Assert.That(
                    (Vector2)_chain.GetActiveSegment(index).View.transform.position,
                    Is.EqualTo(stoppedPositions[index]));
            }

            for (var step = 1; step <= 100; step++)
            {
                _chain.SimulatePathStep(stoppedHead + new Vector2(step * 0.04f, 0f));
            }

            Assert.That(_chain.ActiveCount, Is.EqualTo(20));
            Assert.That(
                (Vector2)_chain.GetActiveSegment(0).View.transform.position,
                Is.Not.EqualTo(stoppedPositions[0]));
            yield return null;
        }

        [UnityTest]
        public IEnumerator VariableFrameSizedSteps_FillPathWithoutLargeSegmentJump()
        {
            Assert.That(_chain.SetDebugSegmentCount(40).IsAccepted, Is.True);
            var stepSizes = new[] { 0.02f, 0.08f, 0.45f, 0.03f, 0.65f, 0.12f, 0.9f, 0.04f };
            var headPosition = Vector2.zero;
            var previous = new Vector2[40];
            for (var index = 0; index < previous.Length; index++)
            {
                previous[index] = _chain.GetActiveSegment(index).View.transform.position;
            }

            for (var cycle = 0; cycle < 40; cycle++)
            {
                foreach (var stepSize in stepSizes)
                {
                    var previousHead = headPosition;
                    headPosition += new Vector2(stepSize, Mathf.Sin(headPosition.x) * stepSize * 0.25f);
                    var headStep = Vector2.Distance(previousHead, headPosition);
                    _chain.SimulatePathStep(headPosition);
                    for (var index = 0; index < previous.Length; index++)
                    {
                        var position = (Vector2)_chain.GetActiveSegment(index).View.transform.position;
                        Assert.That(
                            Vector2.Distance(previous[index], position),
                            Is.LessThanOrEqualTo(headStep + 0.13f));
                        previous[index] = position;
                    }
                }
            }

            yield return null;
        }
    }
}
