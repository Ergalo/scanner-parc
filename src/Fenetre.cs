// La fenêtre : une explication, le bouton qui lance le relevé, la liste à cocher rangée par famille, puis « Voir ce qui
// sera enregistré » et « Enregistrer le fichier ». Pas de logo, pas d'usage, pas de formule ni de prix : tout cela
// n'existe que sur la page du questionnaire.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Ergalo.Releve
{
    internal sealed class Fenetre : Form
    {
        private static readonly Color Fond = ColorTranslator.FromHtml("#fafaf7");
        private static readonly Color Encre = ColorTranslator.FromHtml("#16171d");
        private static readonly Color Texte = ColorTranslator.FromHtml("#3a3d49");
        private static readonly Color Discret = ColorTranslator.FromHtml("#5e6272");
        private static readonly Color Indigo = ColorTranslator.FromHtml("#4b4ded");
        private static readonly Color Bordure = ColorTranslator.FromHtml("#e6e4f7");

        private readonly Environnement env;
        private readonly Catalogue catalogue;
        private readonly Marque marque;
        private Releve releve;
        private bool miseAJour;

        private readonly Label explication = new Label();
        private readonly Label etat = new Label();
        private readonly TreeView liste = new TreeView();
        private readonly Button lancer = new Button();
        private readonly Button voir = new Button();
        private readonly Button enregistrer = new Button();

        public Fenetre(Environnement env, Catalogue catalogue, Marque marque)
        {
            this.env = env;
            this.catalogue = catalogue;
            this.marque = marque;

            Text = "Ergalo · Relevé de vos outils";
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Fond;
            ForeColor = Texte;
            ClientSize = new Size(820, 680);
            MinimumSize = new Size(640, 520);
            StartPosition = FormStartPosition.CenterScreen;

            var titre = new Label
            {
                Text = "Relevé de vos outils de travail",
                Font = new Font("Segoe UI Semibold", 15f),
                ForeColor = Encre,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8),
            };

            explication.Text =
                "Ce programme repère sur ce PC les logiciels et les sites que vous utilisez pour travailler. " +
                "Il ne se connecte pas à Internet et ne lit ni vos mots de passe, ni vos fichiers, ni le contenu des pages : " +
                "des sites visités, il ne garde que le nom (par exemple notion.so) et un nombre de visites sur 90 jours.\n\n" +
                "Rien ne part sans vous : vous décochez ce qui est personnel, vous voyez le fichier, vous l'enregistrez sur ce PC, " +
                "puis vous le déposez vous-même dans votre questionnaire.";
            explication.Dock = DockStyle.Fill;
            explication.AutoSize = true;
            explication.Margin = new Padding(0, 0, 0, 12);

            Bouton(lancer, "Lancer le relevé", true);
            lancer.Click += Lancer;

            etat.AutoSize = true;
            etat.Dock = DockStyle.Fill;
            etat.TextAlign = ContentAlignment.MiddleLeft;
            etat.ForeColor = Discret;
            etat.Margin = new Padding(16, 0, 0, 0);
            etat.Text = "Le relevé prend en général moins d'une minute. Vous pouvez laisser vos navigateurs ouverts.";

            liste.CheckBoxes = true;
            liste.Dock = DockStyle.Fill;
            liste.Margin = new Padding(0, 12, 0, 12);
            liste.BorderStyle = BorderStyle.FixedSingle;
            liste.BackColor = Color.White;
            liste.ForeColor = Encre;
            liste.ShowLines = false;
            liste.ItemHeight = 26;
            liste.Enabled = false;
            liste.AfterCheck += Coche;

            Bouton(voir, "Voir ce qui sera enregistré", false);
            voir.Enabled = false;
            voir.Click += (s, e) => Apercu();

            Bouton(enregistrer, "Enregistrer le fichier", true);
            enregistrer.Anchor = AnchorStyles.Right;
            enregistrer.Enabled = false;
            enregistrer.Click += (s, e) => Enregistrer();

            // Mise en page en lignes : le texte s'adapte à la largeur et à l'échelle d'affichage, la liste prend le reste.
            var ligneLancer = Ligne(2);
            ligneLancer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            ligneLancer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            ligneLancer.Controls.Add(lancer, 0, 0);
            ligneLancer.Controls.Add(etat, 1, 0);

            var ligneBas = Ligne(2);
            ligneBas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            ligneBas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            ligneBas.Controls.Add(voir, 0, 0);
            ligneBas.Controls.Add(enregistrer, 1, 0);

            var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(24, 18, 24, 18) };
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 5; i++) page.RowStyles.Add(i == 3 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
            page.Controls.Add(titre, 0, 0);
            page.Controls.Add(explication, 0, 1);
            page.Controls.Add(ligneLancer, 0, 2);
            page.Controls.Add(liste, 0, 3);
            page.Controls.Add(ligneBas, 0, 4);
            Controls.Add(page);
        }

        private static TableLayoutPanel Ligne(int colonnes)
        {
            return new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = colonnes,
                RowCount = 1,
                Margin = new Padding(0),
            };
        }

        private static void Bouton(Button b, string texte, bool principal)
        {
            b.Text = texte;
            b.AutoSize = true;
            b.MinimumSize = new Size(160, 38);
            b.Padding = new Padding(10, 2, 10, 2);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = principal ? Indigo : Bordure;
            b.BackColor = principal ? Indigo : Color.White;
            b.ForeColor = principal ? Color.White : Encre;
            b.Cursor = Cursors.Hand;
        }

        private async void Lancer(object sender, EventArgs e)
        {
            lancer.Enabled = false;
            etat.Text = "Relevé en cours…";
            try
            {
                releve = await Task.Run(() => Releve.Faire(env, catalogue));
            }
            catch (Exception ex)
            {
                etat.Text = "Le relevé n'a pas pu se faire : " + ex.Message;
                lancer.Enabled = true;
                return;
            }
            Remplir();
            lancer.Text = "Refaire le relevé";
            lancer.Enabled = true;
        }

        private void Remplir()
        {
            miseAJour = true;
            liste.BeginUpdate();
            liste.Nodes.Clear();

            // Outils reconnus, par famille, dans l'ordre du catalogue : une ligne par outil.
            var parOutil = releve.Elements.Where(x => x.Outil != null).GroupBy(x => x.Outil).ToList();
            foreach (var famille in catalogue.Familles)
            {
                var outils = parOutil.Where(g => g.Key.Famille == famille.Key).OrderBy(g => g.Key.Nom).ToList();
                if (outils.Count == 0) continue;
                TreeNode groupe = Groupe(famille.Value);
                foreach (var g in outils) Feuille(groupe, g.Key.Nom + "   ·   " + Detail(g.ToList()), g.ToList());
            }

            Inconnus("Sites souvent visités, non reconnus : cochez ceux qui servent au travail", Source.Navigateur,
                x => x.Domaine + "   ·   " + x.Visites + " visites (" + string.Join(", ", x.NavigateursVus.Select(NomNavigateur)) + ")");
            Inconnus("Autres logiciels installés", Source.Programme, x => x.Nom);
            Inconnus("Autres programmes lancés au démarrage", Source.Demarrage, x => x.Nom);
            Inconnus("Autres extensions de navigateur", Source.Extension, x => x.Nom + " (" + NomNavigateur(x.Navigateur) + ")");

            foreach (TreeNode n in liste.Nodes) MajGroupe(n);
            // Les familles d'outils reconnus sont ouvertes ; les listes de choses inconnues restent fermées.
            foreach (TreeNode n in liste.Nodes) if (n.Checked) n.Expand();
            liste.EndUpdate();
            if (liste.Nodes.Count > 0) liste.TopNode = liste.Nodes[0];
            miseAJour = false;

            liste.Enabled = true;
            voir.Enabled = true;
            enregistrer.Enabled = true;

            int reconnus = parOutil.Count;
            var message = new StringBuilder();
            message.Append(reconnus + " outils reconnus, cochés. ");
            message.Append("Navigateurs lus : " + (releve.NavigateursTrouves.Count == 0 ? "aucun" : string.Join(", ", releve.NavigateursTrouves.Select(NomNavigateur))) + ".");
            if (releve.NavigateursIllisibles.Count > 0)
                message.Append(" Historique illisible pour " + string.Join(", ", releve.NavigateursIllisibles.Select(NomNavigateur)) +
                               " : fermez ce navigateur, puis cliquez sur « Refaire le relevé ».");
            etat.Text = message.ToString();
        }

        private void Inconnus(string titre, Source source, Func<Element, string> libelle)
        {
            var elements = releve.Elements.Where(x => x.Outil == null && x.Source == source).ToList();
            if (elements.Count == 0) return;
            TreeNode groupe = Groupe(titre + " (" + elements.Count + ")");
            foreach (Element x in elements) Feuille(groupe, libelle(x), new List<Element> { x });
        }

        private TreeNode Groupe(string titre)
        {
            var n = new TreeNode(titre) { NodeFont = new Font(liste.Font, FontStyle.Bold) };
            liste.Nodes.Add(n);
            return n;
        }

        private static void Feuille(TreeNode groupe, string texte, List<Element> elements)
        {
            groupe.Nodes.Add(new TreeNode(texte) { Tag = elements, Checked = elements.Any(x => x.Coche) });
        }

        private static string Detail(List<Element> elements)
        {
            var parts = new List<string>();
            if (elements.Any(x => x.Source == Source.Programme)) parts.Add("logiciel installé");
            if (elements.Any(x => x.Source == Source.Demarrage)) parts.Add("lancé au démarrage");
            if (elements.Any(x => x.Source == Source.Extension)) parts.Add("extension");
            long visites = elements.Where(x => x.Source == Source.Navigateur).Sum(x => x.Visites);
            if (visites > 0) parts.Add("site, " + visites + " visites");
            return string.Join(" · ", parts);
        }

        public static string NomNavigateur(string id)
        {
            switch (id)
            {
                case "chrome": return "Chrome";
                case "edge": return "Edge";
                case "firefox": return "Firefox";
                case "brave": return "Brave";
                case "opera": return "Opera";
                case "opera-gx": return "Opera GX";
                case "vivaldi": return "Vivaldi";
                case "arc": return "Arc";
                case "chromium": return "Chromium";
                case "yandex": return "Yandex";
                case "librewolf": return "LibreWolf";
                case "waterfox": return "Waterfox";
                default: return id.Length > 0 ? char.ToUpperInvariant(id[0]) + id.Substring(1) : id;
            }
        }

        private void Coche(object sender, TreeViewEventArgs e)
        {
            if (miseAJour) return;
            miseAJour = true;
            var elements = e.Node.Tag as List<Element>;
            if (elements != null)
            {
                foreach (Element x in elements) x.Coche = e.Node.Checked;
                MajGroupe(e.Node.Parent);
            }
            else
            {
                // Un groupe coché ou décoché entraîne toutes ses lignes.
                foreach (TreeNode f in e.Node.Nodes)
                {
                    f.Checked = e.Node.Checked;
                    foreach (Element x in (List<Element>)f.Tag) x.Coche = e.Node.Checked;
                }
            }
            miseAJour = false;
        }

        private static void MajGroupe(TreeNode groupe)
        {
            if (groupe == null) return;
            groupe.Checked = groupe.Nodes.Cast<TreeNode>().Any(f => f.Checked);
        }

        private string Contenu()
        {
            return Export.Json(marque, releve.Elements, DateTimeOffset.Now);
        }

        private void Apercu()
        {
            var f = new Form
            {
                Text = "Ce qui sera enregistré dans le fichier",
                Font = Font,
                ClientSize = new Size(760, 560),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Fond,
            };
            var note = new Label
            {
                Text = "Voici le fichier tel qu'il sera enregistré, mot pour mot. Il ne contient que les lignes cochées.",
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(12, 12, 12, 0),
            };
            var texte = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 9.5f),
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Text = Contenu().Replace("\n", "\r\n"),
            };
            f.Controls.Add(texte);
            f.Controls.Add(note);
            f.ShowDialog(this);
        }

        private void Enregistrer()
        {
            using (var choix = new SaveFileDialog())
            {
                choix.Title = "Enregistrer le fichier du relevé";
                choix.Filter = "Fichier du relevé (*.json)|*.json";
                choix.FileName = Export.NomFichier(DateTime.Now);
                choix.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (choix.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(choix.FileName, Contenu(), new UTF8Encoding(false));
                var reponse = MessageBox.Show(this,
                    "Le fichier est enregistré :\n" + Path.GetFileName(choix.FileName) + "\n\n" +
                    "Déposez-le maintenant dans votre questionnaire, à l'étape « Listing de votre environnement digital ». " +
                    "Vous y préciserez à quoi sert chaque outil.\n\nOuvrir le dossier du fichier ?",
                    "Fichier enregistré", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (reponse == DialogResult.Yes) Process.Start("explorer.exe", "/select,\"" + choix.FileName + "\"");
            }
        }
    }
}
