using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;

namespace Arch.Suite.Host
{
    internal static class ProjectIndex
    {
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)]
        private static extern int sqlite3_open16(string filename,out IntPtr db);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]
        private static extern int sqlite3_exec(IntPtr db,byte[] sql,IntPtr callback,IntPtr context,IntPtr error);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg16(IntPtr db);
        private static string Value(string s)=>s==null?"NULL":"'"+s.Replace("'","''")+"'";
        internal static string Rebuild(Database drawing,Model model)
        {
            var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArchitecturalSuite","Index");
            Directory.CreateDirectory(root);
            var path=Path.Combine(root,Guid.Parse(drawing.FingerprintGuid).ToString("N")+".sqlite");
            var sql=new StringBuilder("BEGIN IMMEDIATE; CREATE TABLE IF NOT EXISTS meta(revision INTEGER, schema_version INTEGER); CREATE TABLE IF NOT EXISTS objects(guid TEXT PRIMARY KEY,kind TEXT,code TEXT,level TEXT,host_guid TEXT); CREATE TABLE IF NOT EXISTS representations(key TEXT PRIMARY KEY,handle TEXT); DELETE FROM meta; DELETE FROM objects; DELETE FROM representations;");
            sql.AppendFormat(CultureInfo.InvariantCulture,"INSERT INTO meta VALUES({0},{1});",model.Revision,model.Schema);
            foreach(var part in model.Parts)sql.Append("INSERT INTO objects VALUES("+string.Join(",",Value(part.Id),Value(part.Kind),Value(part.Code),Value(part.Level),Value(part.HostId))+");");
            foreach(var map in new[]{model.Outputs,model.Details,model.Tables})
                foreach(var item in map)sql.Append("INSERT INTO representations VALUES("+Value(item.Key)+","+Value(item.Value.Handle)+");");
            sql.Append("COMMIT;");
            IntPtr db;var rc=sqlite3_open16(path,out db);
            try{
                if(rc!=0||sqlite3_exec(db,Encoding.UTF8.GetBytes(sql.ToString()+"\0"),IntPtr.Zero,IntPtr.Zero,IntPtr.Zero)!=0)
                    throw new IOException(db==IntPtr.Zero?"Không mở được SQLite.":Marshal.PtrToStringUni(sqlite3_errmsg16(db)));
            }finally{if(db!=IntPtr.Zero)sqlite3_close(db);}
            return path;
        }
    }
}

