using AS4.ParserService.Models;
using Eu.EDelivery.AS4.Strategies.Retriever;

namespace AS4.ParserService.Services;

public partial class EncodeService
{
    private class InMemoryPayloadRetriever : IPayloadRetriever
    {
        private readonly PayloadInfo _payload;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryPayloadRetriever"/> class.
        /// </summary>
        public InMemoryPayloadRetriever(PayloadInfo payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            _payload = payload;
        }

        /// <summary>
        /// Retrieve <see cref="Stream"/> contents from a given <paramref name="location"/>.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        public Task<Stream> RetrievePayloadAsync(string location, CancellationToken cancellation)
        {
            return Task.FromResult<Stream>(new MemoryStream(_payload.Content));
        }
    }
}
