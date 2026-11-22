using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    internal sealed partial class BoardSurface
    {
        internal const float XiangqiMotionSeconds = .40f;
        private const float XiangqiPickupEnd = .20f;
        private const float XiangqiTravelEnd = .78f;
        private BoardMove xiangqiMotionMove = BoardMove.None;
        private IBoardPosition xiangqiMotionBoard;
        private float xiangqiMotionStartedAt, xiangqiMotionProgress;
        private Text xiangqiMotionLabel;
        private XiangqiPieceLayer xiangqiPieces;

        private int XiangqiMotionDestination => game == BoardGameId.Xiangqi && ReferenceEquals(position, xiangqiMotionBoard)
            ? xiangqiMotionMove.To : -1;

        private void BindXiangqiPieceLayer()
        {
            if (xiangqiPieces == null)
            {
                var layer = UiFactory.Rect(transform, "Xiangqi pieces", 0, 0, 0, 0);
                UiFactory.Stretch(layer);
                xiangqiPieces = layer.gameObject.AddComponent<XiangqiPieceLayer>();
                xiangqiPieces.raycastTarget = false;
            }
            // Keep the river text below the pieces and the fallback piece names above them.
            xiangqiPieces.transform.SetAsLastSibling();
            xiangqiPieces.gameObject.SetActive(true);
            xiangqiPieces.Bind(this);
        }

        internal void BindXiangqiMotion(BoardMove move, float startedAt)
        {
            if (xiangqiMotionMove != BoardMove.None)
            {
                EndXiangqiMotion();
                xiangqiPieces.SetVerticesDirty();
            }
            if (game != BoardGameId.Xiangqi || position == null || move.From == move.To ||
                (uint)move.From >= position.PointCount || (uint)move.To >= position.PointCount ||
                position.PieceAt(move.To, ownSide).Kind <= 0) return;

            float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / XiangqiMotionSeconds);
            if (progress >= 1) return;
            xiangqiMotionMove = move;
            xiangqiMotionBoard = position;
            xiangqiMotionStartedAt = startedAt;
            xiangqiMotionProgress = progress;
            if (!HasXiangqiArtwork)
            {
                string name = XiangqiPieceLabelName(move.To);
                xiangqiMotionLabel = labels.Find(label => label.name == name);
            }
            UpdateXiangqiMotionLabel();
            xiangqiPieces.SetVerticesDirty();
        }

        private void UpdateXiangqiMotion()
        {
            if (xiangqiMotionMove == BoardMove.None) return;
            xiangqiMotionProgress = Mathf.Clamp01((Time.unscaledTime - xiangqiMotionStartedAt) / XiangqiMotionSeconds);
            if (XiangqiMotionDestination < 0 || xiangqiMotionProgress >= 1) EndXiangqiMotion();
            else UpdateXiangqiMotionLabel();
            xiangqiPieces.SetVerticesDirty();
        }

        private void EndXiangqiMotion()
        {
            xiangqiMotionProgress = 1;
            if (XiangqiMotionDestination >= 0) UpdateXiangqiMotionLabel();
            xiangqiMotionMove = BoardMove.None;
            xiangqiMotionBoard = null;
            xiangqiMotionLabel = null;
        }

        private void DrawXiangqiMotion(VertexHelper mesh, int destination)
        {
            if (destination < 0) return;
            Vector2 at = XiangqiMotionCenter(out Vector2 ground, out float lift);
            if (lift > 0)
            {
                Vector2 shadow = ground + new Vector2(.065f, -.10f) * (Pitch * lift);
                Disc(mesh, shadow, Pitch * (.42f + .09f * lift), new Color(.15f, .09f, .04f, .12f * lift));
                Disc(mesh, shadow, Pitch * (.37f + .07f * lift), new Color(.15f, .09f, .04f, .18f * lift));
            }
            DrawXiangqiPiece(mesh, at, VisiblePiece(destination), Pitch * XiangqiMotionScale(lift));
        }

        private void DrawXiangqiPiece(VertexHelper mesh, Vector2 at, BoardPiece piece, float pitch)
        {
            if (piece.Kind <= 0) return;
            if (HasXiangqiArtwork)
            {
                int tile = piece.Side * 7 + piece.Kind - 1;
                float size = pitch * .96f;
                var bounds = new Rect(at.x - size * .5f, at.y - size * .5f, size, size);
                ArtworkQuad(mesh, bounds, new RectInt(632 + tile % 3 * 128, 8 + tile / 3 * 128, 128, 128));
                return;
            }
            float scale = pitch / Pitch;
            Disc(mesh, at, pitch * .393f, UiFactory.Hex("FCF1D5"));
            var ring = piece.Side == 0 ? Red : UiFactory.Ink;
            Ring(mesh, at, pitch * .392f, 2.4f * scale, ring);
            Ring(mesh, at, pitch * .336f, 1.2f * scale, Color.Lerp(Light, ring, .43f));
        }

        private void UpdateXiangqiMotionLabel()
        {
            if (xiangqiMotionLabel == null) return;
            Vector2 at = XiangqiMotionCenter(out _, out float lift);
            float scale = XiangqiMotionScale(lift);
            Rect area = rectTransform.rect;
            RectTransform label = xiangqiMotionLabel.rectTransform;
            Vector2 half = label.rect.size * (.5f * scale);
            label.anchoredPosition = new Vector2(at.x - area.xMin - half.x, at.y - area.yMax + half.y);
            label.localScale = new Vector3(scale, scale, 1);
        }

        private Vector2 XiangqiMotionCenter(out Vector2 ground, out float lift)
        {
            SampleXiangqiMotion(xiangqiMotionProgress, out float travel, out lift);
            ground = Vector2.Lerp(Center(xiangqiMotionMove.From), Center(xiangqiMotionMove.To), travel);
            return ground + Vector2.up * (Pitch * .16f * lift);
        }

        private static float XiangqiMotionScale(float lift) => 1 + .14f * lift;

        private static string XiangqiPieceLabelName(int point) => "Xiangqi piece label " + point;

        private static void SampleXiangqiMotion(float progress, out float travel, out float lift)
        {
            float pickup = progress / XiangqiPickupEnd;
            float slide = (progress - XiangqiPickupEnd) / (XiangqiTravelEnd - XiangqiPickupEnd);
            float landing = (progress - XiangqiTravelEnd) / (1 - XiangqiTravelEnd);
            travel = Mathf.SmoothStep(0, 1, slide);
            lift = Mathf.SmoothStep(0, 1, pickup) * Mathf.SmoothStep(1, 0, landing);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class XiangqiPieceLayer : MaskableGraphic
    {
        private BoardSurface surface;
        public override Texture mainTexture => surface != null ? surface.mainTexture : base.mainTexture;

        internal void Bind(BoardSurface board)
        {
            surface = board;
            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (surface != null) surface.DrawPieces(mesh);
        }
    }
}
