using UnityEngine;
using XanaduYonder.Core.HappyBall;

namespace XanaduYonder.Runtime.HappyBall
{
    // Only visual motion lives here. Detached parts never acquire colliders or model ownership.
    internal sealed class HappyBallEffects
    {
        private const int SplitCapacity = 8;
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private readonly Split[] splits = new Split[SplitCapacity];
        private readonly HappyBallSceneGeometry.Builder builder;
        private readonly HappyBallPaint paint;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private int nextSplit;

        private sealed class Half
        {
            public Transform Root;
            public Mesh Mesh;
            public MeshRenderer Renderer;
        }

        private sealed class Split
        {
            public Half Left;
            public Half Right;
            public bool Active;
            public bool Heavy;
            public float Age;
            public Vector3 Origin;
            public Quaternion Rotation;
        }

        public int ActiveSplitCount
        {
            get { var count = 0; foreach (var split in splits) if (split.Active) count++; return count; }
        }

        public HappyBallEffects(Transform stage, Material material, HappyBallSceneGeometry.Builder geometry, HappyBallPaint paint)
        {
            builder = geometry;
            this.paint = paint;
            for (var i = 0; i < splits.Length; i++)
            {
                splits[i] = new Split
                {
                    Left = CreateHalf(stage, material, "HappyBall split " + i + " left"),
                    Right = CreateHalf(stage, material, "HappyBall split " + i + " right")
                };
            }
        }

        public void Begin(HappyBallLayer layer, float localY, double towerAngle, Quaternion rotation, bool heavy)
        {
            var slot = -1;
            for (var i = 0; i < splits.Length; i++)
                if (!splits[i].Active) { slot = i; break; }
            if (slot < 0) slot = nextSplit;
            nextSplit = (slot + 1) % splits.Length;
            var split = splits[slot];
            paint.ClearDetached(split.Left.Root);
            paint.ClearDetached(split.Right.Root);
            split.Active = true;
            split.Heavy = heavy;
            split.Age = 0;
            split.Origin = new Vector3(0, localY, 0);
            // Captured once in stage coordinates; these nodes are never parented to the rotating tower.
            split.Rotation = rotation;
            builder.Layer(split.Left.Mesh, layer, -1, towerAngle, heavy);
            builder.Layer(split.Right.Mesh, layer, 1, towerAngle, heavy);
            paint.TransferToSplit(layer.Index, towerAngle, split.Left.Root, split.Right.Root);
            Apply(split, split.Left, -1);
            Apply(split, split.Right, 1);
        }

        public void Tick(float dt)
        {
            foreach (var split in splits)
            {
                if (!split.Active) continue;
                split.Age += dt;
                if (split.Age >= Duration(split))
                {
                    split.Active = false;
                    paint.ClearDetached(split.Left.Root);
                    paint.ClearDetached(split.Right.Root);
                    split.Left.Root.gameObject.SetActive(false);
                    split.Right.Root.gameObject.SetActive(false);
                    continue;
                }
                Apply(split, split.Left, -1);
                Apply(split, split.Right, 1);
            }
        }

        private void Apply(Split split, Half half, int side)
        {
            half.Root.gameObject.SetActive(half.Mesh.vertexCount > 0);
            var duration = Duration(split);
            var t = Mathf.Clamp01(split.Age / duration);
            var opening = Mathf.Clamp01(split.Age / (split.Heavy ? .12f : .18f));
            opening = 1 - (1 - opening) * (1 - opening);
            var falling = Mathf.Max(0, split.Age - .09f);
            var distance = opening * (split.Heavy ? 1.4f : .78f) + falling * (split.Heavy ? 1.5f : .65f);
            var fall = falling * falling * (split.Heavy ? 13f : 7.5f);
            half.Root.localPosition = split.Origin + new Vector3(side * distance, -fall, -(split.Heavy ? .18f : .06f) * opening);
            // Tilt is in camera space, then apply the frozen tower orientation to the original mesh.
            half.Root.localRotation = Quaternion.Euler((split.Heavy ? 48 : 16) * t,
                side * (split.Heavy ? 31 : 9) * t, -side * (split.Heavy ? 80 : 31) * t) * split.Rotation;
            var opacity = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.46f, 1, t));
            properties.SetFloat(OpacityId, opacity);
            half.Renderer.SetPropertyBlock(properties);
            paint.FadeDetached(half.Root, opacity);
        }

        public void Rebase(float shift)
        {
            foreach (var split in splits)
            {
                if (!split.Active) continue;
                split.Origin += Vector3.up * shift;
                split.Left.Root.localPosition += Vector3.up * shift;
                split.Right.Root.localPosition += Vector3.up * shift;
            }
        }

        public void Clear()
        {
            foreach (var split in splits)
            {
                split.Active = false;
                paint.ClearDetached(split.Left.Root);
                paint.ClearDetached(split.Right.Root);
                split.Left.Root.gameObject.SetActive(false);
                split.Right.Root.gameObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            foreach (var split in splits)
            {
                Object.Destroy(split.Left.Mesh);
                Object.Destroy(split.Right.Mesh);
            }
        }

        private static float Duration(Split split) { return split.Heavy ? .57f : .64f; }

        private static Half CreateHalf(Transform stage, Material material, string name)
        {
            var mesh = new Mesh { name = name + " mesh" };
            mesh.MarkDynamic();
            var root = HappyBallSceneView.Piece(name, stage, mesh, material);
            root.gameObject.SetActive(false);
            return new Half { Root = root, Mesh = mesh, Renderer = root.GetComponent<MeshRenderer>() };
        }
    }
}
