// Lecture d'une base SQLite (historique des navigateurs) avec winsqlite3.dll, la bibliothèque SQLite livrée dans
// Windows 10 et 11 : rien à embarquer, rien à installer. Lecture seule, sur une copie du fichier.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Ergalo.Releve
{
    internal static class Sqlite
    {
        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_DONE = 101;
        private const int SQLITE_OPEN_READONLY = 0x00000001;

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int sqlite3_open_v2(byte[] nomUtf8, out IntPtr db, int options, IntPtr vfs);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int sqlite3_close_v2(IntPtr db);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int sqlite3_prepare16_v2(IntPtr db, [MarshalAs(UnmanagedType.LPWStr)] string sql, int octets, out IntPtr stmt, IntPtr reste);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int sqlite3_bind_int64(IntPtr stmt, int index, long valeur);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int sqlite3_step(IntPtr stmt);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr sqlite3_column_text16(IntPtr stmt, int colonne);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern long sqlite3_column_int64(IntPtr stmt, int colonne);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int sqlite3_finalize(IntPtr stmt);

        /// <summary>
        /// Exécute une requête qui renvoie (texte, nombre) et passe chaque ligne à <paramref name="ligne"/>.
        /// Les paramètres sont liés dans l'ordre (?1, ?2…). Lève une exception si la base est illisible.
        /// </summary>
        public static void Lire(string chemin, string sql, long[] parametres, Action<string, long> ligne)
        {
            IntPtr db;
            byte[] nom = Encoding.UTF8.GetBytes(chemin + "\0");
            int rc = sqlite3_open_v2(nom, out db, SQLITE_OPEN_READONLY, IntPtr.Zero);
            if (rc != SQLITE_OK)
            {
                if (db != IntPtr.Zero) sqlite3_close_v2(db);
                throw new InvalidOperationException("Base illisible (code " + rc + ")");
            }
            try
            {
                IntPtr stmt;
                rc = sqlite3_prepare16_v2(db, sql, -1, out stmt, IntPtr.Zero);
                if (rc != SQLITE_OK) throw new InvalidOperationException("Requête refusée (code " + rc + ")");
                try
                {
                    for (int i = 0; i < parametres.Length; i++) sqlite3_bind_int64(stmt, i + 1, parametres[i]);
                    while (true)
                    {
                        rc = sqlite3_step(stmt);
                        if (rc == SQLITE_DONE) break;
                        if (rc != SQLITE_ROW) throw new InvalidOperationException("Lecture interrompue (code " + rc + ")");
                        IntPtr texte = sqlite3_column_text16(stmt, 0);
                        string valeur = texte == IntPtr.Zero ? "" : Marshal.PtrToStringUni(texte);
                        ligne(valeur, sqlite3_column_int64(stmt, 1));
                    }
                }
                finally
                {
                    sqlite3_finalize(stmt);
                }
            }
            finally
            {
                sqlite3_close_v2(db);
            }
        }
    }
}
