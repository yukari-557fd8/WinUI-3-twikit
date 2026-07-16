using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace App2.Controls
{
    public sealed partial class TweetUserRow : UserControl
    {
        public TweetUserRow()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty UserNameProperty =
            DependencyProperty.Register(
                nameof(UserName),
                typeof(string),
                typeof(TweetUserRow),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty UserScreenNameProperty =
            DependencyProperty.Register(
                nameof(UserScreenName),
                typeof(string),
                typeof(TweetUserRow),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty CreatedAtAbsoluteDisplayProperty =
            DependencyProperty.Register(
                nameof(CreatedAtAbsoluteDisplay),
                typeof(string),
                typeof(TweetUserRow),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty CreatedAtRelativeDisplayProperty =
            DependencyProperty.Register(
                nameof(CreatedAtRelativeDisplay),
                typeof(string),
                typeof(TweetUserRow),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty UserProfileImageProperty =
            DependencyProperty.Register(
                nameof(UserProfileImage),
                typeof(ImageSource),
                typeof(TweetUserRow),
                new PropertyMetadata(null));

        public static readonly DependencyProperty IsUserProtectedProperty =
            DependencyProperty.Register(
                nameof(IsUserProtected),
                typeof(bool),
                typeof(TweetUserRow),
                new PropertyMetadata(false));

        public string UserName
        {
            get => (string)GetValue(UserNameProperty);
            set => SetValue(UserNameProperty, value);
        }

        public string UserScreenName
        {
            get => (string)GetValue(UserScreenNameProperty);
            set => SetValue(UserScreenNameProperty, value);
        }

        public string CreatedAtAbsoluteDisplay
        {
            get => (string)GetValue(CreatedAtAbsoluteDisplayProperty);
            set => SetValue(CreatedAtAbsoluteDisplayProperty, value);
        }

        public string CreatedAtRelativeDisplay
        {
            get => (string)GetValue(CreatedAtRelativeDisplayProperty);
            set => SetValue(CreatedAtRelativeDisplayProperty, value);
        }

        public ImageSource? UserProfileImage
        {
            get => (ImageSource?)GetValue(UserProfileImageProperty);
            set => SetValue(UserProfileImageProperty, value);
        }

        public bool IsUserProtected
        {
            get => (bool)GetValue(IsUserProtectedProperty);
            set => SetValue(IsUserProtectedProperty, value);
        }
    }
}