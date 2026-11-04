namespace Echo_Windows.Core;

/// <summary>Tracks whether live transcript updates should keep the view at the newest entry.</summary>
public sealed class TranscriptFollowState
{
    public bool IsAtEnd { get; private set; } = true;
    public bool HasNewContent { get; private set; }

    public bool ContentChanged()
    {
        if (IsAtEnd)
        {
            HasNewContent = false;
            return true;
        }

        HasNewContent = true;
        return false;
    }

    public void ViewChanged(bool isAtEnd, bool isUserScrolling)
    {
        if (isAtEnd)
        {
            IsAtEnd = true;
            HasNewContent = false;
        }
        else if (isUserScrolling)
        {
            IsAtEnd = false;
        }
    }

    public void ReturnToEnd()
    {
        IsAtEnd = true;
        HasNewContent = false;
    }
}
