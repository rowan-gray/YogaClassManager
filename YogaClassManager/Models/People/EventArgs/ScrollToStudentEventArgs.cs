namespace YogaClassManager.Models.People.EventArguments;

public class ScrollToIndexEventArgs : EventArgs
{
    private readonly int index;
    private readonly bool isAnimation;

    public ScrollToIndexEventArgs(int index, bool isAnimation)
    {
        this.index = index;
        this.isAnimation = isAnimation;
    }

    public int GetIndex()
    {
        return index;
    }

    public bool ShouldAnimate()
    {
        return isAnimation;
    }
}