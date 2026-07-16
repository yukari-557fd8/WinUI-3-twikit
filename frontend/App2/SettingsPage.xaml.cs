using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace App2
{
    public sealed partial class SettingsPage : Page
    {
        private readonly string? jsonPath = RepositoryPaths.TryGetCookiesFilePath();

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public SettingsPage()
        {
            this.InitializeComponent();

            LoadJsonValues();
        }

        private void LoadJsonValues()
        {
            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
            {
                return;
            }

            var json = File.ReadAllText(jsonPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (dict == null) return;

            if (dict.TryGetValue("auth_token", out var auth))
            {
                TextBoxA.Text = auth;
            }

            if (dict.TryGetValue("ct0", out var ct0))
            {
                TextBoxB.Text = ct0;
            }
        }

        private void OnApplyClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(jsonPath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(jsonPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var dict = new Dictionary<string, string>
            {
                ["auth_token"] = TextBoxA.Text,
                ["ct0"] = TextBoxB.Text
            };

            File.WriteAllText(jsonPath, JsonSerializer.Serialize(dict, JsonOptions));
        }
    }
}