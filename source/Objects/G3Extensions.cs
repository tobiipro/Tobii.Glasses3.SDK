using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace G3SDK
{
    public static class G3Extensions
    {
        private static readonly char[] _validFolderCharacters = { '-', '.' };

        public static string MakeValidFolderName(string folderName)
        {
            var sb = new StringBuilder(folderName.Length);
            foreach (var c in folderName)
            {
                if (char.IsLetterOrDigit(c) || _validFolderCharacters.Contains(c))
                    sb.Append(c);
                else
                    sb.Append('-');
            }

            return sb.ToString();
        }

        /// <summary>
        /// This method will first try to set the folder name and then verify that the setting succeeded.
        /// </summary>
        /// <remarks>Folder names are validated on the device side and will only allow alphanumerical characters, minus (-) and dot (.).</remarks>
        /// <param name="value">The folder name</param>
        /// <returns>True if the folder name actually changed</returns>
        public static async Task<bool> SetFolderAndVerify(this IRecorder recorder, string value)
        {
            if (await recorder.SetFolder(value))
                return false;
            return await recorder.Folder == value;
        }

        /// <summary>
        /// This method will convert the input to a valid folder name using "MakeValidFolderName"
        /// </summary>
        /// <param name="value">The folder name</param>
        /// <returns>True if the setting of the folder name was successful</returns>
        public static Task<bool> SetFolderSafe(this IRecorder recorder, string value)
        {
            return SetFolderAndVerify(recorder, MakeValidFolderName(value));
        }
    }
}