using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Xml.Linq;
using System.ComponentModel;
using System.Windows.Media;
using System.Collections.Generic;
using System.Windows.Threading;
using System.Windows.Media.Animation;

namespace WinPostInstal
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly string _packagesFilePath = "packages.config";
        private string _buttonText;
        private string _installButtonText;
        private bool _isChocolateyInstalled;
        private bool _isDeployButtonEnabled;
        private bool _isSelectAllButtonEnabled;

        public string ButtonText
        {
            get => _buttonText;
            set
            {
                _buttonText = value;
                OnPropertyChanged(nameof(ButtonText));
            }
        }

				private void ShowOverlay_Click(object sender, RoutedEventArgs e)
        {
            Overlay.Visibility = Visibility.Visible;
        }

        private void CloseOverlay_Click(object sender, RoutedEventArgs e)
        {
            Overlay.Visibility = Visibility.Collapsed;
        }

        public string InstallButtonText
        {
            get => _installButtonText;
            set
            {
                _installButtonText = value;
                OnPropertyChanged(nameof(InstallButtonText));
            }
        }

        public bool IsDeployButtonEnabled
        {
            get => _isDeployButtonEnabled;
            set
            {
                _isDeployButtonEnabled = value;
                OnPropertyChanged(nameof(IsDeployButtonEnabled));
            }
        }

        public bool IsSelectAllButtonEnabled
        {
            get => _isSelectAllButtonEnabled;
            set
            {
                _isSelectAllButtonEnabled = value;
                OnPropertyChanged(nameof(IsSelectAllButtonEnabled));
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            ButtonText = (string)FindResource("SelectAllText");
            LoadPackages();
            UpdateInstallButtonText();
            DataContext = this;
            UpdateButtonStates();
            CheckChocolateyStatus();
        }

        private void CheckChocolateyStatus()
        {
            string chocolateyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey");
            string cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ChocolateyHttpCache");

            if (Directory.Exists(chocolateyPath) || Directory.Exists(cachePath))
            {
                UpdateStatus((string)FindResource("msgChocoFound"), new SolidColorBrush(Color.FromRgb(0, 180, 0)), true);
            }
            else
            {
                UpdateStatus((string)FindResource("msgChocoNotFound"), new SolidColorBrush(Color.FromRgb(240, 0, 0)), false);
            }
        }

        private void UpdateStatus(string message, Brush color = null, bool useFadeOut = true)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = color ?? Brushes.Black;
            StatusTextBlock.BeginAnimation(UIElement.OpacityProperty, null);
            StatusTextBlock.Visibility = Visibility.Visible;
            StatusTextBlock.Opacity = 1;

            if (useFadeOut)
            {
                DispatcherTimer timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    StartFadeOutAnimation();
                };
                timer.Start();
            }
        }

        private void StartFadeOutAnimation()
        {
            DoubleAnimation fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(1.0)),
                FillBehavior = FillBehavior.HoldEnd
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                StatusTextBlock.Visibility = Visibility.Collapsed;
            };

            StatusTextBlock.Visibility = Visibility.Visible;
            StatusTextBlock.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
        }

        private void LoadPackages()
        {
            if (File.Exists(_packagesFilePath))
            {
                try
                {
                    XDocument doc = XDocument.Load(_packagesFilePath);
                    var packages = doc.Descendants("package")
                        .Select(pkg => new
                        {
                            Id = pkg.Attribute("id")?.Value,
                            Name = pkg.Attribute("name")?.Value
                        })
                        .Where(p => !string.IsNullOrEmpty(p.Id))
                        .ToList();

                    for (int i = 0; i < packages.Count; i++)
                    {
                        var package = packages[i];
                        bool isFirstOrLast = (i == 0 || i == packages.Count - 1);
                        string displayName = !string.IsNullOrEmpty(package.Name) ? package.Name : package.Id;

                        CheckBox checkBox = new CheckBox
                        {
                            Content = $"   {displayName}",
                            Tag = package.Id,
                            Margin = new Thickness(70, 5, 5, 8),
                            Template = (ControlTemplate)FindResource("CustomCheckBoxTemplate"),
                            Height = isFirstOrLast ? 35 : 30,
                            FontSize = 14
                        };

                        checkBox.Checked += (s, e) => UpdateButtonStates();
                        checkBox.Unchecked += (s, e) => UpdateButtonStates();

                        CheckBoxContainer.Children.Add(checkBox);

                        if (i < packages.Count - 1)
                        {
                            Border separator = new Border
                            {
                                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                                Height = 1,
                                Margin = new Thickness(0, 0, 0, 0)
                            };

                            CheckBoxContainer.Children.Add(separator);
                        }
                    }
                }
                catch (Exception)
                {
                    UpdateStatus((string)FindResource("msgReadErr"), new SolidColorBrush(Color.FromRgb(240, 0, 0)));
                }
            }
            else
            {
                UpdateStatus((string)FindResource("msgFileNotFound"), new SolidColorBrush(Color.FromRgb(240, 0, 0)));
            }

            UpdateButtonStates();
        }

        private void DeployButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsDeployButtonEnabled) return;

            var selectedPackages = new List<string>();

            foreach (var child in CheckBoxContainer.Children)
            {
                if (child is CheckBox checkBox && checkBox.IsChecked == true)
                {
                    string packageId = checkBox.Tag.ToString();
                    selectedPackages.Add(packageId);
                }
            }

            if (selectedPackages.Any())
            {
                string packagesToInstall = string.Join(" ", selectedPackages);
                string command = $"choco install {packagesToInstall} -y";

                var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    Verb = "runas"
                };

                try
                {
                    using (var process = Process.Start(processInfo))
                    {
                        process.WaitForExit();
                    }
                }
                catch (Exception)
                {
                    UpdateStatus((string)FindResource("msgCommandError"), new SolidColorBrush(Color.FromRgb(240, 0, 0)));
                }
            }
            else
            {
                UpdateStatus((string)FindResource("msgItems"), new SolidColorBrush(Color.FromRgb(240, 0, 0)));
            }

            UpdateButtonStates();
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsSelectAllButtonEnabled) return;

            bool allChecked = CheckBoxContainer.Children.OfType<CheckBox>().All(cb => cb.IsChecked == true);

            foreach (CheckBox checkBox in CheckBoxContainer.Children.OfType<CheckBox>())
            {
                checkBox.IsChecked = !allChecked;
            }

            ButtonText = allChecked ? (string)FindResource("SelectAllText") : (string)FindResource("DeselectAllText");

            UpdateButtonStates();
        }

        private void UpdateInstallButtonText()
        {
            string chocolateyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey");
            string cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ChocolateyHttpCache");

            _isChocolateyInstalled = Directory.Exists(chocolateyPath) || Directory.Exists(cachePath);

            InstallButtonText = _isChocolateyInstalled ? (string)FindResource("ChocoManagerDelete") : (string)FindResource("ChocoManagerInstall");

            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool isPackagesFileAvailable = File.Exists(_packagesFilePath);
            bool hasSelectedPackages = CheckBoxContainer.Children.OfType<CheckBox>().Any(cb => cb.IsChecked == true);

            IsSelectAllButtonEnabled = _isChocolateyInstalled && isPackagesFileAvailable;
            IsDeployButtonEnabled = _isChocolateyInstalled && isPackagesFileAvailable && hasSelectedPackages;

            SelectAllButton.Style = IsSelectAllButtonEnabled ? (Style)FindResource("DefaultButton") : (Style)FindResource("NoactiveButton");
            DeployButton.Style = IsDeployButtonEnabled ? (Style)FindResource("AccentButton") : (Style)FindResource("NoactiveButton");
        }

        private void chocoButton_Click(object sender, RoutedEventArgs e)
        {
            string chocolateyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey");
            string cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ChocolateyHttpCache");

            if (Directory.Exists(chocolateyPath) || Directory.Exists(cachePath))
            {
                try
                {
                    string deleteCommand = "";

                    if (Directory.Exists(chocolateyPath))
                    {
                        deleteCommand += $"rmdir /s /q \"{chocolateyPath}\" && ";
                    }
                    if (Directory.Exists(cachePath))
                    {
                        deleteCommand += $"rmdir /s /q \"{cachePath}\" && ";
                    }

                    if (deleteCommand.EndsWith(" && "))
                    {
                        deleteCommand = deleteCommand.Substring(0, deleteCommand.Length - 4);
                    }

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd",
                        Arguments = $"/C {deleteCommand}",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        Verb = "runas"
                    }).WaitForExit();

                    UpdateStatus((string)FindResource("msgChocoDeleted"), new SolidColorBrush(Color.FromRgb(0, 180, 0)));
                }
                catch (Exception)
                {
                    UpdateStatus((string)FindResource("msgAdminRules"), new SolidColorBrush(Color.FromRgb(240, 0, 0)));
                }
            }
            else
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd",
                        Arguments = "/C @powershell -NoProfile -ExecutionPolicy unrestricted -Command \"iex ((new-object net.webclient).DownloadString('https://chocolatey.org/install.ps1'))\" && SET PATH=%PATH%;%systemdrive%\\chocolatey\\bin",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        Verb = "runas"
                    }).WaitForExit();

                    UpdateInstallButtonText();
                    UpdateStatus((string)FindResource("msgChocoInstalled"), new SolidColorBrush(Color.FromRgb(0, 180, 0)));
                }
                catch (Exception ex)
                {
                    UpdateStatus($"ERROR: {ex.Message}", new SolidColorBrush(Color.FromRgb(255, 0, 0)));
                }
            }

            UpdateInstallButtonText();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = "/C @powershell -NoProfile -ExecutionPolicy unrestricted -Command \"irm https://get.activated.win | iex\"",
                UseShellExecute = true,
                CreateNoWindow = false,
                Verb = "runas"
            });
        }
    }
}
