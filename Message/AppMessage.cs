using CommunityToolkit.Mvvm.Messaging.Messages;

namespace MediaOrganizeViewer.Messages
{
    public class FolderSelectedMessage : ValueChangedMessage<string>
    {
        public bool IsSource { get; }
        public FolderSelectedMessage(string path, bool isSource) : base(path) => IsSource = isSource;
    }

    public class RootPathChangedMessage : ValueChangedMessage<string>
    {
        public bool IsSource { get; }
        public RootPathChangedMessage(string path, bool isSource) : base(path) => IsSource = isSource;
    }
}