namespace Eu.EDelivery.AS4.Utilities;

public class FilenameUtils
{
    public static string EnsureValidFilename(string filename)
    {
        return string.Join(string.Empty, filename.Split(Path.GetInvalidFileNameChars()));
    }

    public static string EnsureFilenameIsUnique(string filename)
    {
        while (File.Exists(filename))
        {
            const string copyExtension = " - Copy";

            var name = Path.GetFileNameWithoutExtension(filename) + copyExtension + Path.GetExtension(filename);
            var copyFilename = Path.Combine(Path.GetDirectoryName(filename) ?? string.Empty, name);

            filename = copyFilename;
        }

        return filename;
    }
}
