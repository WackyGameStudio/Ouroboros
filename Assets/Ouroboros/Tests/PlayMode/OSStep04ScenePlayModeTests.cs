using System.Collections;
using NUnit.Framework;
using Ouroboros.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSStep04ScenePlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameSceneContainsMovementCollisionVisualAndClampedCameraFoundation()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var player = Object.FindAnyObjectByType<OSPlayerController>();
            var visual = Object.FindAnyObjectByType<OSPlayerHeadVisual>();
            var follower = Object.FindAnyObjectByType<OSCameraFollower>();
            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var body = player != null ? player.GetComponent<Rigidbody2D>() : null;
            var solid = player != null ? player.GetComponent<CircleCollider2D>() : null;
            var obstacles = GameObject.Find("Obstacles");

            Assert.That(player, Is.Not.Null);
            Assert.That(visual, Is.Not.Null);
            Assert.That(follower, Is.Not.Null);
            Assert.That(session, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(solid, Is.Not.Null);
            Assert.That(obstacles, Is.Not.Null);
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
            Assert.That(body.interpolation, Is.EqualTo(RigidbodyInterpolation2D.Interpolate));
            Assert.That(solid.isTrigger, Is.False);
            Assert.That(player.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("PlayerHeadSolid")));
            Assert.That(obstacles.transform.Find("Obstacle_Wide"), Is.Not.Null);
            Assert.That(obstacles.transform.Find("Obstacle_Tall"), Is.Not.Null);
            Assert.That(obstacles.transform.Find("Obstacle_Block"), Is.Not.Null);
            Assert.That(obstacles.transform.Find("Obstacle_West"), Is.Not.Null);
            Assert.That(obstacles.transform.Find("Obstacle_East"), Is.Not.Null);
            Assert.That(obstacles.transform.Find("Obstacle_South"), Is.Not.Null);
            Assert.That(obstacles.transform.Find("Obstacle_North"), Is.Not.Null);
            Assert.That(player.WorldMin, Is.EqualTo(new Vector2(-24f, -15f)));
            Assert.That(player.WorldMax, Is.EqualTo(new Vector2(24f, 15f)));
            Assert.That(player.transform.Find("DirectionIndicator"), Is.Not.Null);
            var headCore = player.transform.Find("Head");
            Assert.That(headCore, Is.Not.Null);
            Assert.That(visual.SpriteForwardDegrees, Is.EqualTo(90f).Within(0.0001f));
            Assert.That(
                Vector2.Dot(((Vector2)headCore.up).normalized, player.LastDirection.normalized),
                Is.GreaterThan(0.999f));
            Assert.That(follower.Target, Is.EqualTo(player.transform));

            var camera = follower.GetComponent<Camera>();
            var originalSize = camera.orthographicSize;
            Assert.That(camera.orthographic, Is.True);
            Assert.That(originalSize, Is.EqualTo(6.5f).Within(0.0001f));
            body.position = player.WorldMax - Vector2.one;
            follower.SnapToTarget();
            yield return null;

            var halfHeight = camera.orthographicSize;
            var halfWidth = halfHeight * camera.aspect;
            Assert.That(camera.orthographicSize, Is.EqualTo(originalSize));
            Assert.That(follower.transform.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(
                follower.transform.position.x + halfWidth + follower.EdgePadding,
                Is.LessThanOrEqualTo(follower.WorldMax.x + 0.0001f));
            Assert.That(
                follower.transform.position.y + halfHeight + follower.EdgePadding,
                Is.LessThanOrEqualTo(follower.WorldMax.y + 0.0001f));
        }
    }
}
