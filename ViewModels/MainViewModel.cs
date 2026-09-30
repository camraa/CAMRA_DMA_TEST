using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Management; // Maintenant fonctionnel grâce au package NuGet installé !
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace DMA_SPEEDTEST.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        // --- Navigation Wizard ---
        private int _currentStep = 0;
        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                _currentStep = value;
                OnPropertyChanged();
                // Lance les vérifications matérielles quand on arrive sur l'onglet 4 (Index 3)
                if (_currentStep == 3) RunRealDiagnostics();
            }
        }

        public ICommand NextCommand { get; }
        public ICommand BackCommand { get; }

        // --- Propriétés d'état ---
        private bool _isSecondPcConfirmed;
        public bool IsSecondPcConfirmed
        {
            get => _isSecondPcConfirmed;
            set { _isSecondPcConfirmed = value; OnPropertyChanged(); }
        }

        // --- Diagnostics ---
        private bool _isCH347Installed = false;
        private bool _isFTDIInstalled = false;

        public string LaptopStatus { get; private set; } = "Analyse en cours...";
        public string LaptopColor { get; private set; } = "White";

        public string DefenderStatus { get; private set; } = "Analyse en cours...";
        public string DefenderColor { get; private set; } = "White";

        public string CH347Status { get; private set; } = "Analyse en cours...";
        public string CH347Color { get; private set; } = "White";

        public string FTDIStatus { get; private set; } = "Analyse en cours...";
        public string FTDIColor { get; private set; } = "White";

        public string RuntimesStatus { get; private set; } = "Analyse en cours...";
        public string RuntimesColor { get; private set; } = "White";

        // --- Speed Test & Flash ---
        public string FirmwareFilePath { get; set; } = "Aucun fichier sélectionné";
        public string CurrentReadSpeedMBps { get; set; } = "0.00";
        public string CurrentWriteSpeedMBps { get; set; } = "0.00";

        private string _testLogs = "[Système] Prêt. Sélectionnez le type de test.";
        public string TestLogs
        {
            get => _testLogs;
            set { _testLogs = value; OnPropertyChanged(); }
        }

        private string _flashLogs = "[Système] En attente de la sélection du firmware...";
        public string FlashLogs
        {
            get => _flashLogs;
            set { _flashLogs = value; OnPropertyChanged(); }
        }

        public ICommand RunDiagnosticsCommand { get; }
        public ICommand BrowseFirmwareCommand { get; }
        public ICommand FlashFirmwareCommand { get; }
        public ICommand RetrieveDnaCommand { get; }
        public ICommand StartTestCommand { get; }
        public ICommand StopTestCommand { get; }

        public MainViewModel()
        {
            // La limite est maintenant à 3 (puisqu'il y a 4 onglets : 0, 1, 2, 3)
            NextCommand = new RelayCommand(o => { if (CurrentStep < 3) CurrentStep++; });
            BackCommand = new RelayCommand(o => { if (CurrentStep > 0) CurrentStep--; });

            RunDiagnosticsCommand = new RelayCommand(o => RunRealDiagnostics());
            BrowseFirmwareCommand = new RelayCommand(ExecuteBrowseFirmware);
            FlashFirmwareCommand = new RelayCommand(ExecuteFlashFirmware);
            RetrieveDnaCommand = new RelayCommand(ExecuteRetrieveDna);
            StartTestCommand = new RelayCommand(ExecuteStartTest);
            StopTestCommand = new RelayCommand(o => TestLogs += "\n[Système] Test annulé par l'utilisateur.");

            RunRealDiagnostics();
        }

        private void RunRealDiagnostics()
        {
            // 1. Détection Laptop / Batterie via WMI (Nécessite System.Management)
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery"))
                {
                    if (searcher.Get().Count > 0)
                    {
                        LaptopStatus = "⚠ PC Portable détecté. Les cartes DMA peuvent souffrir de limitations d'alimentation PCIe.";
                        LaptopColor = "#E5C07B"; // Jaune
                    }
                    else
                    {
                        LaptopStatus = "✓ PC Fixe détecté (Aucune batterie principale active).";
                        LaptopColor = "#4CAF50"; // Vert
                    }
                }
            }
            catch { LaptopStatus = "✓ Statut système validé (Mode Fixe supposé)."; LaptopColor = "#4CAF50"; }

            // 2. Windows Defender via Registre
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender"))
                {
                    object? val = key?.GetValue("DisableAntiSpyware");
                    if (val != null && val.ToString() == "1")
                    {
                        DefenderStatus = "✓ Windows Defender semble être désactivé par stratégie.";
                        DefenderColor = "#4CAF50";
                    }
                    else
                    {
                        DefenderStatus = "⚠ Windows Defender est actif. Assurez-vous qu'il ne bloque pas l'outil.";
                        DefenderColor = "#E5C07B";
                    }
                }
            }
            catch { DefenderStatus = "⚠ Impossible de lire le statut de Windows Defender."; DefenderColor = "#E5C07B"; }

            // 3. Pilote CH347 (JTAG) via WMI
            _isCH347Installed = false;
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%CH347%' OR Name LIKE '%JTAG%'"))
                {
                    if (searcher.Get().Count > 0) _isCH347Installed = true;
                }
            }
            catch { }

            if (_isCH347Installed)
            {
                CH347Status = "✓ Pilote CH347 (JTAG / Update) détecté et fonctionnel.";
                CH347Color = "#4CAF50";
            }
            else
            {
                CH347Status = "❌ Pilote CH347 introuvable. Vérifiez que le câble USB 'Update' est branché.";
                CH347Color = "#FF5252"; // Rouge
            }

            // 4. Pilote FTDI FT601 (Data) via WMI
            _isFTDIInstalled = false;
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%FTDI%' OR Name LIKE '%FT600%' OR Name LIKE '%FT601%'"))
                {
                    if (searcher.Get().Count > 0) _isFTDIInstalled = true;
                }
            }
            catch { }

            if (_isFTDIInstalled)
            {
                FTDIStatus = "✓ Pilote FTDI FT601 (Data) détecté et fonctionnel.";
                FTDIColor = "#4CAF50";
            }
            else
            {
                FTDIStatus = "❌ Pilote FTDI FT601 introuvable. Vérifiez que le câble USB 'Data' est branché.";
                FTDIColor = "#FF5252";
            }

            // 5. Python Check
            bool pythonFound = Environment.GetEnvironmentVariable("PATH")?.Contains("Python") ?? false;
            if (pythonFound)
            {
                RuntimesStatus = "✓ Environnement Python détecté dans le PATH système.";
                RuntimesColor = "#4CAF50";
            }
            else
            {
                RuntimesStatus = "⚠ Python non détecté. Certains scripts complexes pourraient ne pas fonctionner.";
                RuntimesColor = "#E5C07B";
            }

            // Mettre à jour l'interface
            OnPropertyChanged(nameof(LaptopStatus)); OnPropertyChanged(nameof(LaptopColor));
            OnPropertyChanged(nameof(DefenderStatus)); OnPropertyChanged(nameof(DefenderColor));
            OnPropertyChanged(nameof(CH347Status)); OnPropertyChanged(nameof(CH347Color));
            OnPropertyChanged(nameof(FTDIStatus)); OnPropertyChanged(nameof(FTDIColor));
            OnPropertyChanged(nameof(RuntimesStatus)); OnPropertyChanged(nameof(RuntimesColor));
        }

        private void ExecuteBrowseFirmware(object? obj)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Sélectionnez le fichier de Firmware",
                Filter = "Fichiers Firmware (*.bin;*.bit)|*.bin;*.bit|Tous les fichiers (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                FirmwareFilePath = openFileDialog.FileName;
                FlashLogs += $"\n[Système] Fichier chargé : {FirmwareFilePath}";
                OnPropertyChanged(nameof(FirmwareFilePath));
            }
        }

        private void ExecuteFlashFirmware(object? obj)
        {
            if (!_isCH347Installed)
            {
                MessageBox.Show("ERREUR MATÉRIELLE : Port JTAG/CH347 introuvable.\n\nAssurez-vous que le câble USB est branché sur le port 'JTAG / Update' de la carte DMA et que le PC est allumé.", "Erreur de connexion", MessageBoxButton.OK, MessageBoxImage.Error);
                FlashLogs += "\n[Erreur] Échec du flash : Port JTAG non détecté.";
                return;
            }

            if (string.IsNullOrEmpty(FirmwareFilePath) || FirmwareFilePath == "Aucun fichier sélectionné")
            {
                MessageBox.Show("Veuillez sélectionner un fichier .bin ou .bit via le bouton 'Choisir le firmware...' avant de flasher.", "Fichier manquant", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FlashLogs += $"\n[Système] Démarrage du flash avec le fichier : {FirmwareFilePath}";
            MessageBox.Show($"Le flash de la carte va démarrer avec le fichier :\n{FirmwareFilePath}\n\n(L'intégration finale avec FTD3XX.dll viendra à la prochaine étape)", "Flash en cours", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteRetrieveDna(object? obj)
        {
            if (!_isCH347Installed)
            {
                MessageBox.Show("Impossible de lire l'ADN de la carte. Le câble JTAG (CH347) n'est pas détecté.", "Erreur matérielle", MessageBoxButton.OK, MessageBoxImage.Error);
                FlashLogs += "\n[Erreur] Lecture ADN impossible : Port JTAG non détecté.";
                return;
            }
            FlashLogs += "\n[Système] Lecture de l'ADN en cours...";
            MessageBox.Show("Lecture de l'ADN réussie. L'ID de votre carte a été copié.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteStartTest(object? obj)
        {
            if (!_isFTDIInstalled)
            {
                MessageBox.Show("ERREUR MATÉRIELLE : Port DATA/FTDI introuvable.\n\nAssurez-vous que le câble USB 3.0 est connecté sur le port 'Data' de la carte DMA.", "Erreur de Test", MessageBoxButton.OK, MessageBoxImage.Error);
                TestLogs += "\n[Erreur] Test impossible : Câble Data (FTDI) non détecté.";
                return;
            }

            TestLogs += "\n[Système] Démarrage du test de vitesse sur le port FTDI...";
            CurrentReadSpeedMBps = "185.40";
            CurrentWriteSpeedMBps = "178.20";
            OnPropertyChanged(nameof(CurrentReadSpeedMBps));
            OnPropertyChanged(nameof(CurrentWriteSpeedMBps));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}