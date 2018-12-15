This library should deliver an easy way to add caching to a class.
The main reason for this functionality is because -on a daily basis- I need to add caching on top of existing classes.
This currently results in multiple custom built implementations that implement typed caching for each specific sub-class separately.

The challenge is to reach the following goals:
1. easily construction of a 'caching' class based on an existing class respecting the same interface.
2. configurable caching policy globally or per method
3. configurable cache key calculation globally or per method
