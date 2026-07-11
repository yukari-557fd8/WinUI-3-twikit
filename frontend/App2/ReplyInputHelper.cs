using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.System;
using Windows.UI.Core;

namespace App2
{
    internal static class ReplyInputHelper
    {
        public static TweetViewModel? FindTweetViewModel(DependencyObject element)
        {
            var current = element;
            while (current != null)
            {
                if (current is FrameworkElement { Tag: TweetViewModel tagVm })
                {
                    return tagVm;
                }

                if (current is ListViewItem listItem)
                {
                    if (listItem.DataContext is TweetViewModel listVm)
                    {
                        return listVm;
                    }

                    if (listItem.Content is TweetViewModel contentVm)
                    {
                        return contentVm;
                    }
                }

                if (current is FrameworkElement fe && fe.DataContext is TweetViewModel vm)
                {
                    return vm;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        public static void SyncReplyTextFromInput(DependencyObject element, TweetViewModel vm)
        {
            var textBox = FindReplyTextBoxInAncestors(element);
            if (textBox != null)
            {
                vm.ReplyText = textBox.Text;
            }
        }

        public static bool IsCtrlEnter(KeyRoutedEventArgs e)
        {
            return e.Key == VirtualKey.Enter && IsControlDown();
        }

        private static bool IsControlDown()
        {
            var leftControl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftControl);
            var rightControl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightControl);
            return leftControl.HasFlag(CoreVirtualKeyStates.Down)
                || rightControl.HasFlag(CoreVirtualKeyStates.Down);
        }

        public static bool TrySendReply(DependencyObject element, Action<object> sendReply)
        {
            var vm = FindTweetViewModel(element);
            if (vm == null || !vm.IsReplying || vm.IsReplySending)
            {
                return false;
            }

            SyncReplyTextFromInput(element, vm);
            if (string.IsNullOrWhiteSpace(vm.ReplyText))
            {
                return false;
            }

            sendReply(element);
            return true;
        }

        private static TextBox? FindReplyTextBoxInAncestors(DependencyObject element)
        {
            var current = element;
            while (current != null)
            {
                var textBox = FindReplyTextBoxInSubtree(current);
                if (textBox != null)
                {
                    return textBox;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static TextBox? FindReplyTextBoxInSubtree(DependencyObject element)
        {
            if (element is TextBox textBox)
            {
                return textBox;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(element);
            for (var i = 0; i < childCount; i++)
            {
                var found = FindReplyTextBoxInSubtree(VisualTreeHelper.GetChild(element, i));
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
