using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Squad
{
    /// <summary>
    /// Per soldier steering toward a slot: acceleration and braking, arrival easing, a rest dead zone (no jitter),
    /// separation from squad mates, push out and slide along Obstacle and Structure colliders, smooth turning.
    /// </summary>
    public class SoldierMotor
    {
        readonly Transform tr;
        static readonly Collider[] hits = new Collider[8];

        public Vector3 Velocity;
        public Vector3 Target;
        public Vector3 RestFacing = Vector3.forward;
        public Vector3 LookTarget;
        public bool HasLookTarget;
        public bool AtRest { get; private set; }
        public float SpeedNormalized { get; private set; }
        public bool Enabled = true;
        int frameOffset;

        public SoldierMotor(Transform t)
        {
            tr = t;
            Target = t.position;
            frameOffset = Random.Range(0, 3);
        }

        public void Tick(float dt, in MotorSettings s, Vector3[] neighbours, int count, int self)
        {
            if (!Enabled || dt <= 0f) return;
            Vector3 pos = tr.position;
            Vector3 to = Target - pos; to.y = 0f;
            float dist = to.magnitude;

            Vector3 desired = Vector3.zero;
            if (dist > s.deadZone)
            {
                float speed = s.maxSpeed * Mathf.Clamp01(dist / Mathf.Max(0.01f, s.arriveRadius));
                speed = Mathf.Max(speed, Mathf.Min(s.maxSpeed, 0.9f));
                speed = Mathf.Min(speed, dist / Mathf.Max(dt, 1e-4f));
                desired = to / dist * speed;
                AtRest = false;
            }

            // Separation: only meaningful overlap counts, so soldiers at their slots do not drift.
            if (neighbours != null)
            {
                float r = s.separationRadius, r2 = r * r;
                Vector3 push = Vector3.zero;
                for (int j = 0; j < count; j++)
                {
                    if (j == self) continue;
                    float dx = pos.x - neighbours[j].x, dz = pos.z - neighbours[j].z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 >= r2) continue;
                    if (d2 < 1e-6f) { dx = (self - j) * 0.01f; dz = 0.01f; d2 = dx * dx + dz * dz; }
                    float d = Mathf.Sqrt(d2);
                    float w = 1f - d / r;
                    push.x += dx / d * w;
                    push.z += dz / d * w;
                }
                if (!AtRest || push.sqrMagnitude > 0.09f) desired += push * s.separationStrength;
            }

            // Obstacles: overlap test, push out and remove the inward velocity component (slide).
            Vector3 correction = Vector3.zero;
            if (((Time.frameCount + frameOffset) % 2) == 0 || !AtRest)
            {
                Vector3 probe = pos; probe.y = 0.4f;
                int n = Physics.OverlapSphereNonAlloc(probe, s.obstacleRadius, hits, Layers.ObstacleMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var col = hits[i];
                    Vector3 cp = col.ClosestPoint(probe);
                    Vector3 away = probe - cp; away.y = 0f;
                    float d = away.magnitude;
                    if (d < 1e-4f) { away = probe - col.bounds.center; away.y = 0f; d = away.magnitude; if (d < 1e-4f) continue; }
                    Vector3 nrm = away / d;
                    float pen = s.obstacleRadius - d;
                    if (pen > 0f) correction += nrm * pen;
                    float into = Vector3.Dot(desired, nrm);
                    if (into < 0f)
                    {
                        desired -= nrm * into;
                        Vector3 tangent = new Vector3(-nrm.z, 0f, nrm.x);
                        if (Vector3.Dot(tangent, to) < 0f) tangent = -tangent;
                        desired += tangent * (-into) * 0.8f;
                    }
                }
            }

            float rate = desired.sqrMagnitude > Velocity.sqrMagnitude ? s.accel : s.decel;
            Velocity = Vector3.MoveTowards(Velocity, desired, rate * dt);
            Velocity.y = 0f;
            if (dist <= s.deadZone && Velocity.sqrMagnitude < 0.04f && correction.sqrMagnitude < 1e-4f)
            {
                Velocity = Vector3.zero;
                AtRest = true;
            }

            pos += Velocity * dt + Vector3.ClampMagnitude(correction, 0.5f);
            pos.x = Mathf.Clamp(pos.x, s.boundsMin.x, s.boundsMax.x);
            pos.z = Mathf.Clamp(pos.z, s.boundsMin.y, s.boundsMax.y);
            pos.y = 0f;

            float spd = Velocity.magnitude;
            SpeedNormalized = spd / Mathf.Max(0.01f, s.maxSpeed);

            Vector3 face;
            if (HasLookTarget && spd < s.maxSpeed * 0.55f) { face = LookTarget - pos; }
            else if (spd > 0.35f) face = Velocity;
            else face = RestFacing;
            face.y = 0f;
            Quaternion rot = tr.rotation;
            if (face.sqrMagnitude > 1e-4f)
                rot = Quaternion.RotateTowards(rot, Quaternion.LookRotation(face), s.turnSpeed * dt);
            tr.SetPositionAndRotation(pos, rot);
        }

        public void Teleport(Vector3 p)
        {
            p.y = 0f;
            tr.position = p;
            Target = p;
            Velocity = Vector3.zero;
        }
    }
}
