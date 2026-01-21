using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaOrganizeViewer.Messages
{
    // フォルダが選択されたことを通知するメッセージ
    public class FolderSelectedMessage : ValueChangedMessage<string>
    {
        public bool IsSource { get; }

        public FolderSelectedMessage(string path, bool isSource) : base(path)
        {
            IsSource = isSource;
        }
    }
}