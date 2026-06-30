using Microsoft.EntityFrameworkCore;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal static class AutoKeyGenerator
    {
        public static async Task<int> NextAsync(IQueryable<int> keys)
        {
            return await keys.AnyAsync()
                ? await keys.MaxAsync() + 1
                : 1;
        }

        public static async Task<int> FirstAvailableAsync(IQueryable<int> keys)
        {
            var usedKeys = await keys
                .Where(key => key > 0)
                .OrderBy(key => key)
                .ToListAsync();

            var nextKey = 1;

            foreach (int key in usedKeys)
            {
                if (key > nextKey)
                {
                    break;
                }

                if (key == nextKey)
                {
                    nextKey++;
                }
            }

            return nextKey;
        }
    }
}
