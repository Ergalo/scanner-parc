// Réduire une adresse au nom du site, et un site inconnu à son domaine principal.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergalo.Releve
{
    internal static class Domaines
    {
        // Suffixes à deux niveaux courants : « exemple.co.uk » et « mairie.gouv.fr » sont des domaines entiers.
        private static readonly HashSet<string> SecondNiveau = new HashSet<string>
        {
            "co", "com", "net", "org", "gov", "gouv", "edu", "ac", "asso", "nom", "tm", "presse", "ne", "or",
        };

        // Noms de réseau local : jamais proposés (ce sont des appareils ou un intranet, pas des outils du catalogue).
        private static readonly string[] SuffixesLocaux = { ".local", ".lan", ".internal", ".home", ".localdomain", ".localhost", ".home.arpa", ".intranet", ".corp" };

        /// <summary>
        /// Nom d'hôte d'une adresse web (http ou https seulement) : minuscules, sans « www. », sans port ni chemin.
        /// Null pour tout le reste : pages internes du navigateur, fichiers locaux, adresses IP, réseau local.
        /// </summary>
        public static string NomHote(string adresse)
        {
            if (string.IsNullOrWhiteSpace(adresse)) return null;
            Uri uri;
            if (!Uri.TryCreate(adresse.Trim(), UriKind.Absolute, out uri)) return null;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
            if (uri.HostNameType != UriHostNameType.Dns) return null;
            string hote = uri.IdnHost.ToLowerInvariant().TrimEnd('.');
            if (hote.StartsWith("www.")) hote = hote.Substring(4);
            if (!hote.Contains('.')) return null;
            foreach (string s in SuffixesLocaux) if (hote.EndsWith(s, StringComparison.Ordinal)) return null;
            return hote;
        }

        /// <summary>
        /// Domaine principal d'un site inconnu : « client42.monoutil.com » donne « monoutil.com ». Sert à regrouper
        /// les sous-domaines et à ne pas faire sortir un nom de compte placé en sous-domaine.
        /// </summary>
        public static string Principal(string hote)
        {
            string[] e = hote.Split('.');
            if (e.Length <= 2) return hote;
            int garder = (e[e.Length - 1].Length == 2 && SecondNiveau.Contains(e[e.Length - 2])) ? 3 : 2;
            return string.Join(".", e.Skip(e.Length - garder));
        }
    }
}
