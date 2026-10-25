namespace TMBS.Core.Input
{
    public interface IUpdatableInput : IBuildInputAdapter
    {
        void Tick(float deltaTime);
    }
}
