// Point d'entrée. Sans marque valide dans son nom de fichier (programme pris ailleurs que dans le questionnaire du client,
// renommé, ou expiré), le programme s'arrête avec une phrase simple. Options réservées aux essais, sur un PC fictif seulement :
//   --essai <dossier>            lit ce dossier au lieu du PC (voir EnvironnementEssai)
//   --sortie <fichier.json>      avec --essai : enregistre le fichier sans fenêtre, lignes reconnues cochées
//   --maintenant <date ISO>      avec --essai : date du jour simulée
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// Informations de version lues par Windows (Propriétés > Détails) et par les antivirus. La version suit l'étiquette de la
// publication (v1.1.0 → 1.1.0.0) ; la changer veut dire recompiler, donc repartir de zéro en réputation : le moins souvent possible.
[assembly: AssemblyTitle("Ergalo · Relevé de vos outils")]
[assembly: AssemblyDescription("Relève les logiciels et les sites de travail de ce PC, sans connexion à Internet. Code source : https://github.com/Ergalo/scanner-parc")]
[assembly: AssemblyCompany("Ergalo SAS")]
[assembly: AssemblyProduct("Ergalo · Relevé de vos outils")]
[assembly: AssemblyCopyright("© 2026 Ergalo SAS · licence MIT")]
[assembly: AssemblyTrademark("Ergalo")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: AssemblyInformationalVersion("1.1.0")]
[assembly: NeutralResourcesLanguage("fr-FR")]

namespace Ergalo.Releve
{
    internal static class Programme
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            string essai = Option(args, "--essai");
            string sortie = Option(args, "--sortie");
            string maintenant = Option(args, "--maintenant");

            bool silencieux = essai != null && sortie != null;
            if (!silencieux)
            {
                try { SetProcessDPIAware(); } catch (Exception) { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
            }

            Marque marque = null;
            try { marque = Marque.LireNom(Path.GetFileName(Assembly.GetEntryAssembly().Location)); }
            catch (Exception) { }

            DateTime maintenantUtc = DateTime.UtcNow;
            if (essai != null && maintenant != null)
                maintenantUtc = DateTimeOffset.Parse(maintenant, CultureInfo.InvariantCulture).UtcDateTime;

            if (marque == null)
                return Arret(silencieux, 2,
                    "Ce programme ne trouve pas son code d'accès, qui est dans le nom du fichier (ergalo-releve-…). " +
                    "S'il a été renommé, rendez-lui son nom d'origine. Sinon, téléchargez-le à nouveau depuis votre questionnaire Ergalo, " +
                    "à l'étape « Listing de votre environnement digital ».");
            if (marque.Expiree(maintenantUtc))
                return Arret(silencieux, 3,
                    "Ce programme a expiré. Retournez sur votre questionnaire Ergalo et téléchargez-le à nouveau : c'est gratuit et immédiat.");

            Catalogue catalogue = Catalogue.Embarque();
            Environnement env = essai != null ? (Environnement)new EnvironnementEssai(essai, maintenantUtc) : new EnvironnementWindows();

            if (silencieux)
            {
                Releve releve = Releve.Faire(env, catalogue);
                var date = new DateTimeOffset(maintenantUtc).ToOffset(TimeZoneInfo.Local.GetUtcOffset(maintenantUtc));
                File.WriteAllText(sortie, Export.Json(marque, releve.Elements, date), new UTF8Encoding(false));
                return 0;
            }

            Application.Run(new Fenetre(env, catalogue, marque));
            return 0;
        }

        private static int Arret(bool silencieux, int code, string message)
        {
            if (silencieux) Console.Error.WriteLine(message);
            else MessageBox.Show(message, "Ergalo · Relevé de vos outils", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return code;
        }

        private static string Option(string[] args, string nom)
        {
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == nom) return args[i + 1];
            return null;
        }
    }
}
