// After: let pureFile, pureSet, _byName = PurityEngine.buildListA ...
// Add:

            let sanity = PuritySanity.check pureSet
            PuritySanity.print sanity

            if not sanity.Ok then
                failwith "Purity sanity check failed: false pures in foundational set."
