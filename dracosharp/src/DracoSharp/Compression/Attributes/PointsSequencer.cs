using DracoSharp.Attributes;

namespace DracoSharp.Compression.Attributes;

public abstract class PointsSequencer
{
    private List<int> _outPointIds;

    public bool GenerateSequence(List<int> outPointIds)
    {
        _outPointIds = outPointIds;
        return GenerateSequenceInternal();
    }

    public void AddPointId(int pointId) => _outPointIds.Add(pointId);

    public virtual bool UpdatePointToAttributeIndexMapping(PointAttribute attribute) => false;

    protected abstract bool GenerateSequenceInternal();
    protected List<int> OutPointIds => _outPointIds;
}
