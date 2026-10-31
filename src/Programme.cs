// Point d'entrée. Le programme est le même pour tout le monde et fonctionne quel que soit son nom (D-451) : c'est la page du
// questionnaire, ouverte par le lien privé du client, qui reçoit le fichier produit. Options réservées aux essais, sur un PC
// fictif seulement :
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
// publication (v1.2.0 → 1.2.0.0) ; la changer veut dire recompiler, donc repartir de zéro en réputation : le moins souvent possible.
[assembly: AssemblyTitle("Ergalo · Relevé de vos outils")]
[assembly: AssemblyDescription("Relève les logiciels et les sites de travail de ce PC, sans connexion à Internet. Code source : https://github.com/Ergalo/scanner-parc")]
[assembly: AssemblyCompany("Ergalo")]
[assembly: AssemblyProduct("Ergalo · Relevé de vos outils")]
[assembly: AssemblyCopyright("© 2026 Guillaume Clerc (Ergalo) · licence MIT")]
[assembly: AssemblyTrademark("Ergalo")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]
[assembly: AssemblyInformationalVersion("1.2.0")]
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

            DateTime maintenantUtc = DateTime.UtcNow;
            if (essai != null && maintenant != null)
                maintenantUtc = DateTimeOffset.Parse(maintenant, CultureInfo.InvariantCulture).UtcDateTime;

            Catalogue catalogue = Catalogue.Embarque();
            Environnement env = essai != null ? (Environnement)new EnvironnementEssai(essai, maintenantUtc) : new EnvironnementWindows();

            if (silencieux)
            {
                Releve releve = Releve.Faire(env, catalogue);
                var date = new DateTimeOffset(maintenantUtc).ToOffset(TimeZoneInfo.Local.GetUtcOffset(maintenantUtc));
                File.WriteAllText(sortie, Export.Json(releve.Elements, date), new UTF8Encoding(false));
                return 0;
            }

            Application.Run(new Fenetre(env, catalogue));
            return 0;
        }

        private static string Option(string[] args, string nom)
        {
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == nom) return args[i + 1];
            return null;
        }
    }
}
