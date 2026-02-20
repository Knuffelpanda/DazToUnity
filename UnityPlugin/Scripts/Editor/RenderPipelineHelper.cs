using UnityEngine;
using UnityEngine.Rendering;

namespace Daz3D
{
    public enum DazRenderPipeline
    {
        BuiltIn,
        URP,
        HDRP
    }

    public static class RenderPipelineHelper
    {
        private static DazRenderPipeline? _cachedPipeline;

        public static DazRenderPipeline CurrentPipeline
        {
            get
            {
                if (!_cachedPipeline.HasValue)
                    _cachedPipeline = DetectPipeline();
                return _cachedPipeline.Value;
            }
        }

        public static bool IsHDRP => CurrentPipeline == DazRenderPipeline.HDRP;
        public static bool IsURP => CurrentPipeline == DazRenderPipeline.URP;
        public static bool IsBuiltIn => CurrentPipeline == DazRenderPipeline.BuiltIn;

        public static void InvalidateCache()
        {
            _cachedPipeline = null;
        }

        private static DazRenderPipeline DetectPipeline()
        {
            var rpAsset = GraphicsSettings.currentRenderPipeline;
            if (rpAsset == null)
                return DazRenderPipeline.BuiltIn;

            string typeName = rpAsset.GetType().ToString();
            if (typeName.Contains("HDRenderPipeline"))
                return DazRenderPipeline.HDRP;
            if (typeName.Contains("UniversalRenderPipeline"))
                return DazRenderPipeline.URP;

            return DazRenderPipeline.BuiltIn;
        }
    }
}
