/*
Copyright (c) 2017 Chair of Computational Modeling and Simulation (CMS), 
Prof. André Borrmann, 
Technische Universität München, 
Arcisstr. 21, D-80333 München, Germany

This file is part of QL4BIMinterpreter.

QL4BIMinterpreter is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
any later version.

QL4BIMinterpreter is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with QL4BIMinterpreter. If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.Collections.Generic;
using System.Linq;
using QL4BIMinterpreter.QL4BIM;
using QL4BIMprimitives;

namespace QL4BIMinterpreter.OperatorsLevel0
{
    public class DereferenceOperator : IDereferenceOperator
    {
        //only symbols and simple types in operators, no nodes
        //symbolTable, parameterSym1, ..., returnSym

        private readonly IInterpreterRepository interpreterRepository;

        public DereferenceOperator(IInterpreterRepository interpreterRepository)
        {
            this.interpreterRepository = interpreterRepository;
        }

        public List<QLEntity[]> ResolveReferenceTuplesIn(IEnumerable<QLEntity[]> tuples, int attributeIndex, bool replace, string referenceName)
        {
            var result = new List<QLEntity[]>();

            foreach (var tuple in tuples)
            {
                var referenceValue = tuple[attributeIndex].GetPropertyValue(referenceName);
                var referencedEntities = EntitesFromRefs(referenceValue);
                var newTuples = referencedEntities.Select(r => replace ? AddEntityReplace(tuple, r) : AddEntity(tuple, r));
                result.AddRange(newTuples);
            }
            return result;
        }

        private  IEnumerable<QLEntity> EntitesFromRefs(QLPart referenceValue)
        {

            if (referenceValue == null || (referenceValue.QLEntityId == null && referenceValue.QLList == null))
                return new List<QLEntity>();

            if (referenceValue.QLEntityId != null)
            {
                var referencedEntity = interpreterRepository.GlobalEntityDictionary[referenceValue.QLEntityId.Id];
                return new List<QLEntity>(){referencedEntity};
            }

            if (referenceValue.QLList != null && referenceValue.QLList.HasRef)
            {
                return referenceValue.QLList.List.Where(r => r.QLEntityId != null).Select(r =>
                    interpreterRepository.GlobalEntityDictionary[r.QLEntityId.Id]).ToList();
            }

            throw new ArgumentException();
        }

        private QLEntity[] AddEntityReplace(QLEntity[] tuple, QLEntity referencedEntity)
        {
            var newTuple = new List<QLEntity>();
            for (int index = 0; index < tuple.Length; index++)
            {
                var qlEntity = tuple[index];
                newTuple.Add(index == tuple.Length-1 ? referencedEntity : qlEntity);
            }
            return newTuple.ToArray();
        }

        private QLEntity[] AddEntity(QLEntity[] tuple, QLEntity referencedEntity)
        {
            var newTuple = new List<QLEntity>(tuple);
            newTuple.Add(referencedEntity);
            return newTuple.ToArray();
        }


        public IEnumerable<QLEntity[]> ResolveReferenceSetIn(IEnumerable<QLEntity> entites, string referenceName)
        {
            var result = new List<QLEntity[]>();
            foreach (var qlEntity in entites)
            {
                var referenceValue = qlEntity.GetPropertyValue(referenceName);
                var referencedEntities = EntitesFromRefs(referenceValue);
                result.AddRange(referencedEntities.Select(r => new QLEntity[] { qlEntity, r }));
            }

            return result;
        }


        public void ReferenceSet(SetSymbol parameterSym1, string[] references, RelationSymbol returnSym)
        {
            Console.WriteLine("Dereferencer'ing...");

            if (references.Length == 1)
            {
                var firstPairs = ResolveReferenceSetIn(parameterSym1.Entites, references[0]);
                returnSym.SetTuples(firstPairs);
            }
            else if (references.Length == 2)
            {   
                //pair original first arg
                var firstPairs = ResolveReferenceSetIn(parameterSym1.Entites, references[0]);

                //pair original second arg
                var secondPairs = ResolveReferenceTuplesIn(firstPairs, 1, true, references[1]);
                returnSym.SetTuples(secondPairs);
            }
            else
            {
                throw new QueryException("The operation Dereferencer-Set has the wrong number of parameters");
            }
        }

        public void ReferenceRelAtt(RelationSymbol parameterSym1, string[] references, RelationSymbol returnSym)
        {
            var index = parameterSym1.Index.Value;
            if (references.Length == 1)
            {
                var firstTuples = ResolveReferenceTuplesIn(parameterSym1.Tuples, index, false, references[0]);
                returnSym.SetTuples(firstTuples);
            }
            else if (references.Length == 2)
            {
                var firstTuples = ResolveReferenceTuplesIn(parameterSym1.Tuples, index,  false, references[0]);
                returnSym.SetTuples(ResolveReferenceTuplesIn(firstTuples, index + 1, true, references[1]));
            }
            else
            {
                throw new QueryException("The operation Dereferencer-relation has the wrong number of parameters");
            }
        }


    }
}
