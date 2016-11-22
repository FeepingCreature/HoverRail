using VRageMath;

namespace HoverRail {
	class SlidingAverageVector {
		double factor;
		public Vector3D value;
		public SlidingAverageVector(double factor) { this.factor = factor; this.value = Vector3D.Zero; }
		public void update(Vector3D newValue) { this.value = this.value * (1 - factor) + newValue * factor; }
	}
}
