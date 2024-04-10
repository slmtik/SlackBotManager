using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace SlackBotManager.API.Core
{
    public static class Extensions
    {
        public static async Task<string> GetStringFromPipe(this PipeReader pipeReader)
        {
            List<string> results = [];

            while (true)
            {
                ReadResult readResult = await pipeReader.ReadAsync();
                var buffer = readResult.Buffer;

                SequencePosition? position;
                do
                {
                    position = buffer.PositionOf((byte)'\n');

                    if (position != null)
                    {
                        var readOnlySequence = buffer.Slice(0, position.Value);
                        AddStringToList(results, in readOnlySequence);

                        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                    }
                }
                while (position != null);

                if (readResult.IsCompleted && buffer.Length > 0)
                    AddStringToList(results, in buffer);

                pipeReader.AdvanceTo(buffer.Start, buffer.End);

                if (readResult.IsCompleted)
                    break;
            }

            return string.Join("\n", results);
        }

        private static void AddStringToList(List<string> results, in ReadOnlySequence<byte> readOnlySequence)
        {
            ReadOnlySpan<byte> span = readOnlySequence.IsSingleSegment ? readOnlySequence.First.Span : readOnlySequence.ToArray().AsSpan();
            results.Add(Encoding.UTF8.GetString(span));
        }
    }
}
