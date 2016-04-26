using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.IO;

namespace KinectTheramin.Database
{
    internal static class DB
    {
        #region Strings
        private static readonly string databaseName = "KinectTheramin.sqlite";
        private static readonly string connectionString = "Data Source="+databaseName+";Version=3;Password=itsSpelledTheremin;";
        private static readonly string databaseCreationScriptFilePath = "Database\\DBCreationScript.txt";
        private static readonly string databaseFillScriptFilePath = "Database\\DBFillScript.txt";
        private static readonly string defaultTuningsFilePath = "Database\\DefaultTunings";

        private static bool DBValid = false;

//        private static readonly string databaseCreationScript = @"
//DROP TABLE IF EXISTS NOTES;
//DROP TABLE IF EXISTS NOTE_FREQUENCIES
//DROP TABLE IF EXISTS SCALES;
//DROP TABLE IF EXISTS SCALE_NOTE_MAP;
//DROP TABLE IF EXISTS TUNINGS;
//
//CREATE TABLE IF NOT EXISTS
//NOTES
//(
//    ID      INTEGER CONSTRAINT primkey PRIMARY KEY ASC AUTOINCREMENT,
//    Name    TEXT
//);
//
//CREATE TABLE IF NOT EXISTS
//NOTE_FREQUENCIES
//(
//    ID      INTEGER CONSTRAINT primkey PRIMARY KEY ASC AUTOINCREMENT,
//    NoteID  TEXT,
//    Octave  INTEGER,
//    Freq    REAL,
//    FOREIGN KEY(NoteID) REFERENCES NOTES(ID)
//);
//
//CREATE TABLE IF NOT EXISTS
//SCALES
//(
//    ID      INTEGER CONSTRAINT primkey PRIMARY KEY ASC AUTOINCREMENT,
//    Name    TEXT,
//)
//
//CREATE TABLE IF NOT EXISTS
//SCALE_NOTE_MAP
//(
//    ID      INTEGER CONSTRAINT primkey PRIMARY KEY ASC AUTOINCREMENT,
//    ScaleID INTEGER,
//    NoteFreqID INTEGER
//    FOREIGN KEY(ScaleID) REFERENCES SCALES(ID),
//    FOREIGN KEY(NoteFreqID) REFERENCES NOTE_FREQUENCIES(ID)
//)
//
//CREATE TABLE IF NOT EXISTS
//TUNINGS
//(
//    Oscillator  REAL,
//    Frequency   REAL,
//    Resistance  INT,
//    CONSTRAINT primkey PRIMARY KEY (Frequency, Oscillator)
//) WITHOUT ROWID;
//";

//        private static readonly string fillNoteFrequenciesTable = @"
//INSERT OR REPLACE INTO NOTE_FREQUENCIES (Name, Octave, Freq)
//VALUES
//('C',0,16.35),
//('C#',0,17.32),
//('D',0,18.35),
//('D#',0,19.45),
//('E',0,20.6),
//('F',0,21.83),
//('F#',0,23.12),
//('G',0,24.5),
//('G#',0,25.96),
//('A',0,27.5),
//('A#',0,29.14),
//('B',0,30.87),
//('C',1,32.7),
//('C#',1,34.65),
//('D',1,36.71),
//('D#',1,38.89),
//('E',1,41.2),
//('F',1,43.65),
//('F#',1,46.25),
//('G',1,49),
//('G#',1,51.91),
//('A',1,55),
//('A#',1,58.27),
//('B',1,61.74),
//('C',2,65.41),
//('C#',2,69.3),
//('D',2,73.42),
//('D#',2,77.78),
//('E',2,82.41),
//('F',2,87.31),
//('F#',2,92.5),
//('G',2,98),
//('G#',2,103.83),
//('A',2,110),
//('A#',2,116.54),
//('B',2,123.47),
//('C',3,130.81),
//('C#',3,138.59),
//('D',3,146.83),
//('D#',3,155.56),
//('E',3,164.81),
//('F',3,174.61),
//('F#',3,185),
//('G',3,196),
//('G#',3,207.65),
//('A',3,220),
//('A#',3,233.08),
//('B',3,246.94),
//('C',4,261.63),
//('C#',4,277.18),
//('D',4,293.66),
//('D#',4,311.13),
//('E',4,329.63),
//('F',4,349.23),
//('F#',4,369.99),
//('G',4,392),
//('G#',4,415.3),
//('A',4,440),
//('A#',4,466.16),
//('B',4,493.88),
//('C',5,523.25),
//('C#',5,554.37),
//('D',5,587.33),
//('D#',5,622.25),
//('E',5,659.25),
//('F',5,698.46),
//('F#',5,739.99),
//('G',5,783.99),
//('G#',5,830.61),
//('A',5,880),
//('A#',5,932.33),
//('B',5,987.77),
//('C',6,1046.5),
//('C#',6,1108.73),
//('D',6,1174.66),
//('D#',6,1244.51),
//('E',6,1318.51),
//('F',6,1396.91),
//('F#',6,1479.98),
//('G',6,1567.98),
//('G#',6,1661.22),
//('A',6,1760),
//('A#',6,1864.66),
//('B',6,1975.53),
//('C',7,2093),
//('C#',7,2217.46),
//('D',7,2349.32),
//('D#',7,2489.02),
//('E',7,2637.02),
//('F',7,2793.83),
//('F#',7,2959.96),
//('G',7,3135.96),
//('G#',7,3322.44),
//('A',7,3520),
//('A#',7,3729.31),
//('B',7,3951.07),
//('C',8,4186.01),
//('C#',8,4434.92),
//('D',8,4698.63),
//('D#',8,4978.03),
//('E',8,5274.04),
//('F',8,5587.65),
//('F#',8,5919.91),
//('G',8,6271.93),
//('G#',8,6644.88),
//('A',8,7040),
//('A#',8,7458.62),
//('B',8,7902.13);         
//";
        #endregion Strings

        internal static bool InitializeDB()
        {
            bool initSuccessful = false;
            //if (File.Exists(databaseName))
            //{
            //    //verify database integrity
            //    try
            //    {
            //        using (SQLiteConnection conn = new SQLiteConnection(connectionString))
            //        using (SQLiteCommand cmd = conn.CreateCommand())
            //        {
            //            conn.Open();
            //            cmd.CommandText = "SELECT 'ok'";
            //            object scalar = cmd.ExecuteScalar();
            //            if (String.Compare(scalar.ToString(), "ok") == 0)
            //            {
            //                initSuccessful = true;
            //            }
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        Log.WriteException("DB", e);
            //    }
            //}
            //else //Database doesn't exist, so create it
            //{
                initSuccessful = CreateDatabase(true);
            //}
                DBValid = initSuccessful;
            return initSuccessful;
        }

        /// <summary>
        /// Creates a fresh database file.
        /// </summary>
        /// <param name="fillInInformation">If true, loads the initial default information into the database</param>
        /// <returns>True if everything was completed successfully.</returns>
        private static bool CreateDatabase(bool fillInInformation)
        {
            bool successful = false;
            if (File.Exists(databaseName))
            {
                File.Delete(databaseName);
            }
            try
            {
                SQLiteConnection.CreateFile(databaseName);
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    conn.Open();
                    cmd.CommandText = File.ReadAllText(databaseCreationScriptFilePath);
                    cmd.ExecuteNonQuery();

                    successful = true;
                    if (fillInInformation)
                    {
                        cmd.CommandText = GenerateFillScript();
                        int rowsInserted = cmd.ExecuteNonQuery();
                        successful = (rowsInserted > 0);
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteException("DB", e);
                successful = false;
            }
            return successful;
        }


        internal static string ExecuteScalar(string query)
        {
            if (DBValid)
            {
                try
                {
                    using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        conn.Open();
                        cmd.CommandText = query;
                        object scalar = cmd.ExecuteScalar();
                        return scalar == null || scalar == DBNull.Value ? null : scalar.ToString();
                    }
                }
                catch (Exception e)
                {
                    Log.WriteException("DB", e);
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        internal static List<double> GetAllNoteFrequencies(ushort[] octaves, short scaleID = -1)
        {
            if (DBValid)
            {
                StringBuilder query = new StringBuilder("SELECT Freq FROM NOTE_FREQUENCIES");
                if (octaves != null && octaves.Length > 0)
                {
                    query.Append(" WHERE Octave IN (");
                    foreach (ushort oct in octaves)
                    {
                        query.Append(oct);
                        query.Append(',');
                    }
                    query.Length--; //removes last comma
                    query.Append(')');
                }
                try
                {
                    List<double> frequencies = new List<double>();
                    using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        conn.Open();
                        cmd.CommandText = query.ToString();
                        SQLiteDataReader reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            frequencies.Add(reader.GetDouble(0));
                        }
                        reader.Close();
                    }
                    return frequencies;
                }
                catch (Exception e)
                {
                    Log.WriteException("DB", e);
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        //TODO: Join with above method
        internal static List<Note> GetOctaveScaleNotes(ushort scaleID, ushort[] octaves)
        {

            if (DBValid)
            {
                StringBuilder query = new StringBuilder(@"
SELECT nf.Freq, n.Name
FROM NOTE_FREQUENCIES nf
    JOIN SCALE_NOTE_MAP snm ON nf.NoteID = snm.NoteID
    JOIN NOTES n ON snm.NoteID = n.ID
WHERE snm.ScaleID = ");
                query.Append(scaleID);
                query.Append(" AND nf.Octave IN (");
                foreach (ushort oct in octaves)
                {
                    query.Append(oct);
                    query.Append(',');
                }
                query.Length--;
                query.Append(')');
                query.Append(" ORDER BY nf.Freq ASC");
                try
                {
                    List<Note> frequencies = new List<Note>();
                    using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        conn.Open();
                        cmd.CommandText = query.ToString();
                        SQLiteDataReader reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            frequencies.Add(new Note() { frequency = reader.GetDouble(0), name = reader.GetString(1) });
                        }
                        reader.Close();
                    }
                    return frequencies;
                }
                catch (Exception e)
                {
                    Log.WriteException("DB", e);
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        internal static uint GetResistanceFromOscFreq(ushort osc, double freq)
        {
            string query = String.Format("SELECT resistance FROM TUNINGS WHERE Oscillator = {0} AND Frequency = {1}", osc, freq);
            string result = DB.ExecuteScalar(query);
            return result == null ? 0 : Convert.ToUInt32(result);
        }

        internal static void UpdateTuning(ushort osc, double freq, uint resistance)
        {
            string query = String.Format("INSERT OR REPLACE INTO TUNINGS VALUES ({0},{1},{2});", osc, freq, resistance);
            if (DBValid)
            {
                try
                {
                    using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        conn.Open();
                        cmd.CommandText = query;
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception e)
                {
                    Log.WriteException("DB", e);
                }
            }
        }

        /// <summary>
        /// Generates script to insert the default tuning information into the db
        /// </summary>
        /// <returns>The generated script</returns>
        private static string GenerateFillScript()
        {
            StringBuilder sb = new StringBuilder(File.ReadAllText(databaseFillScriptFilePath));
            sb.AppendLine("INSERT INTO TUNINGS (Oscillator, Frequency, Resistance) VALUES");
            List<string> lines = File.ReadLines(defaultTuningsFilePath).ToList<string>();
            foreach (string line in lines)
            {
                string[] values = line.Split(',');
                sb.Append('(');
                sb.Append(values[0]);
                sb.Append(',');
                sb.Append(values[1]);
                sb.Append(',');
                sb.Append(values[2]);
                sb.Append("),");
            }
            sb.Length--; //Trims the last character to remove the final comma
            return sb.ToString(); 
        }
    }
}
