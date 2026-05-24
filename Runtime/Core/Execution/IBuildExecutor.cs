using TMBS.Core.Pipeline;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Execution
{
    public interface IBuildExecutor
    {
        void Execute(in PipelineContext ctx, Tilemap targetTilemap);
    }
}