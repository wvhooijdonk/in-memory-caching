The goal of this library is to deliver an easy way to add caching on top of an existing class.
Currently I'm developing this solution on a case by case basis which result in separate implementations.

The challenge is to reach the following goals:
1. easily construction of a 'caching' class based on an existing class respecting the same interface. (done, but might be improved)
2. configurable caching policy globally or per method (work in progress)
3. configurable cache key calculation globally or per method (work in progress)
