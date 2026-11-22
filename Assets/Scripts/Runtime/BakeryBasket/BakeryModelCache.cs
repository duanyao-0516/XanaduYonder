using System;
using System.Collections.Generic;
using UnityEngine;
using XanaduYonder.Core.BakeryBasket;
using Object = UnityEngine.Object;

namespace XanaduYonder.Runtime.BakeryBasket
{
    // Piece geometry is immutable across the two baskets and across rounds in one visit.
    internal sealed class BakeryModelCache : IDisposable
    {
        internal readonly GameObject[] Prefabs = new GameObject[BakeryRound.KindCount];
        internal readonly BakeryFood.Shape[] Shapes = new BakeryFood.Shape[BakeryRound.KindCount];
        internal readonly Bounds?[] Bounds = new Bounds?[BakeryRound.KindCount];
        internal readonly Mesh[][] Hulls = new Mesh[BakeryRound.KindCount][];
        internal readonly Dictionary<Mesh, BakeryMesh> Picks = new Dictionary<Mesh, BakeryMesh>();
        internal readonly Dictionary<Mesh, Mesh> Outlines = new Dictionary<Mesh, Mesh>();
        private readonly List<Mesh> owned = new List<Mesh>();

        internal BakeryModelCache(BakeryThemeCatalog catalog)
        {
            for (int kind = 0; kind < Shapes.Length; kind++)
            {
                if (catalog.Theme == BakeryTheme.Pastry && BakeryFood.IsImported(kind))
                {
                    Prefabs[kind] = Resources.Load<GameObject>("BakeryBasket/Models/" + catalog.Names[kind]);
                    continue;
                }
                var shape = catalog.CreateShape(kind);
                Shapes[kind] = shape;
                Own(shape.Surface);
                owned.AddRange(shape.ContactParts);
            }
        }

        internal Mesh Own(Mesh mesh)
        {
            owned.Add(mesh);
            return mesh;
        }

        public void Dispose()
        {
            foreach (var mesh in owned) Object.Destroy(mesh);
            owned.Clear();
        }
    }
}
