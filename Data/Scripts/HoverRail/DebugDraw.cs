using VRageMath;

namespace HoverRail {
	static class DebugDraw {
		const bool DEBUG = false;
		public static void Sphere(Vector3D pos, float radius, Color color) {
			if (!DEBUG) return;
			var matrix = MatrixD.CreateTranslation(pos);
			var rasterizer = MySimpleObjectRasterizer.Solid;
			if (radius > 1) rasterizer = MySimpleObjectRasterizer.SolidAndWireframe;
			MySimpleObjectDraw.DrawTransparentSphere(ref matrix, radius, ref color, rasterizer, 30);
		}
		public static void Line(Vector3D from, Vector3D to, float radius) {
			if (!DEBUG) return;
			Vector4 color = new Vector4(1, 1, 1, 1);
			MySimpleObjectDraw.DrawLine(from, to, null, ref color, radius);
		}
	}
}
