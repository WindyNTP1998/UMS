using UMS.Platform.Common.Extensions;

namespace UMS.Platform.Common.Utils;

public static partial class Util
{
    public static class Pager
    {
        /// <summary>
        ///     Support execute async action paged. <br />
        ///     executeFn: (int skipCount, int pageSize) =>
        /// </summary>
        /// <param name="executeFn">Execute function async. Input is: skipCount, pageSize.</param>
        /// <param name="maxItemCount">Max items count</param>
        /// <param name="pageSize">Page size to execute.</param>
        /// <returns>Task.</returns>
        public static async Task ExecutePagingAsync(Func<int, int, Task> executeFn,
            long maxItemCount,
            int pageSize)
        {
            var currentSkipItems = 0;

            do
            {
                await executeFn(currentSkipItems, pageSize);
                currentSkipItems += pageSize;

                GC.Collect();
            } while (currentSkipItems < maxItemCount);
        }

        /// <summary>
        ///     Support execute async action paged with return value
        /// </summary>
        /// <param name="executeFn">Execute function async. Input is: skipCount, pageSize.</param>
        /// <param name="maxItemCount">Max items count</param>
        /// <param name="pageSize">Page size to execute.</param>
        /// <returns>Task.</returns>
        public static async Task<List<TPagedResult>> ExecutePagingAsync<TPagedResult>(
            Func<int, int, Task<TPagedResult>> executeFn,
            long maxItemCount,
            int pageSize)
        {
            var currentSkipItems = 0;
            var result = new List<TPagedResult>();

            do
            {
                var pagedResult = await executeFn(currentSkipItems, pageSize);

                result.Add(pagedResult);

                currentSkipItems += pageSize;

                GC.Collect();
            } while (currentSkipItems < maxItemCount);

            return result;
        }

        /// <summary>
        ///     Execute until the executeFn return no items
        /// </summary>
        public static Task ExecuteScrollingPagingAsync<TItem>(Func<Task<IEnumerable<TItem>>> executeFn,
            int maxExecutionCount)
        {
            return ExecuteScrollingPagingAsync(() => executeFn().Then(_ => _.ToList()), maxExecutionCount);
        }

        /// <summary>
        ///     Execute until the executeFn return no items
        /// </summary>
        public static async Task ExecuteScrollingPagingAsync<TItem>(Func<Task<List<TItem>>> executeFn,
            int maxExecutionCount)
        {
            var executionItemsResult = await executeFn();
            var totalExecutionCount = 1;

            while (totalExecutionCount <= maxExecutionCount && executionItemsResult.Any())
            {
                executionItemsResult = await executeFn();
                totalExecutionCount += 1;

                GC.Collect();
            }
        }
    }
}