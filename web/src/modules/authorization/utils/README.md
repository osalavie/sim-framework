# Authorization Utils

This directory contains utility functions for the authorization module.

## Query Parameters (`queryParams.ts`)

Utilities for building ABP-compatible query parameters for API requests.

### Functions

#### `buildSortingString(sortFields: SortInfo[]): string | undefined`

Builds an ABP-formatted sorting string from an array of sort fields.

**Example:**
```typescript
import { buildSortingString } from '@/modules/authorization/utils'

const sorts = [
  { field: 'name', order: 'ascend' },
  { field: 'creationTime', order: 'descend' }
]

const sortingString = buildSortingString(sorts)
// Returns: "name asc, creationTime desc"
```

#### `buildQueryParams(options): BaseQueryParams & T`

Builds a complete query parameters object for API requests.

**Example:**
```typescript
import { buildQueryParams } from '@/modules/authorization/utils'

const params = buildQueryParams({
  pagination: { page: 2, pageSize: 20 },
  keyword: 'search term',
  sortFields: [{ field: 'name', order: 'ascend' }],
  additionalParams: { moduleId: '123' }
})

// Returns:
// {
//   skipCount: 20,
//   maxResultCount: 20,
//   keyword: 'search term',
//   sorting: 'name asc',
//   moduleId: '123'
// }
```

#### `calculatePageCount(totalCount: number, pageSize: number): number`

Calculates the total number of pages based on total count and page size.

**Example:**
```typescript
import { calculatePageCount } from '@/modules/authorization/utils'

const pageCount = calculatePageCount(95, 10)
// Returns: 10 (rounds up)
```

### Usage in Composables

Here's how to use these utilities in a data management composable:

```typescript
import { ref, onMounted } from 'vue'
import { buildQueryParams, calculatePageCount, type SortInfo } from '@/modules/authorization/utils'
import { userApi } from '@/modules/authorization/api/user'

export function useUserData() {
  const loading = ref(false)
  const users = ref([])
  const searchKeyword = ref('')
  const sortFields = ref<SortInfo[]>([])
  
  const pagination = ref({
    page: 1,
    pageSize: 10,
    pageCount: 1,
    itemCount: 0,
  })

  const loadUsers = async () => {
    loading.value = true
    try {
      // Build query parameters using the utility
      const params = buildQueryParams({
        pagination: pagination.value,
        keyword: searchKeyword.value,
        sortFields: sortFields.value,
      })
      
      const response = await userApi.getList(params)
      users.value = response.items || []
      
      // Calculate page count using the utility
      pagination.value.pageCount = calculatePageCount(
        response.totalCount || 0,
        pagination.value.pageSize
      )
      pagination.value.itemCount = response.totalCount || 0
    } catch (error) {
      console.error('Failed to load users:', error)
    } finally {
      loading.value = false
    }
  }

  const handleSortChange = (sorts: SortInfo[]) => {
    sortFields.value = sorts
    pagination.value.page = 1
    loadUsers()
  }

  onMounted(() => {
    loadUsers()
  })

  return {
    loading,
    users,
    searchKeyword,
    sortFields,
    pagination,
    loadUsers,
    handleSortChange,
  }
}
```

### Benefits

1. **Consistency**: All composables use the same query parameter building logic
2. **Type Safety**: TypeScript types ensure correct parameter structure
3. **Maintainability**: Changes to query parameter format only need to be made in one place
4. **Testability**: Utility functions are easy to unit test
5. **Reusability**: Can be used across all entity management composables
