using Business.Abstract;
using Core.Messages;
using Core.Results;
using DataAccess.Abstract;
using Entities.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Business.Concrete
{
    //To see relevant messages, check Core.Messages.Messages.cs
    public class ContainerManager : IContainerService
    {
        //Considering the possibility of using a different database in the future, injection was made with an interface. 
        IContainerDal _containerDal;

        public ContainerManager(IContainerDal containerDal)
        {
            _containerDal = containerDal;
        }

        public IDataResult<List<List<Container>>> GetOptimizedContainers(long vehicleId, int clusterNumber)
        {
            try
            {
                List<List<Container>> list = _containerDal.GetOptimezedClusteredContainers(vehicleId, clusterNumber).ToList();
                return new SuccessDataResult<List<List<Container>>>(list,Messages.ContainersClustered);
            }
            catch (Exception)
            {
                return new ErrorDataResult<List<List<Container>>> (Messages.CheckInputs);
            }
        }
    }
}
