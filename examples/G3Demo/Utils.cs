using System.IO;
using System.Media;
using System.Reflection;

namespace G3Demo
{
    public class Utils
    {
        public static void Play(string file)
        {
            var path = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var fileName = $"{path}\\sounds\\{file}.wav";
            using (var player = new SoundPlayer(fileName))
            {
                player.Play();
            }
        }

    }
}