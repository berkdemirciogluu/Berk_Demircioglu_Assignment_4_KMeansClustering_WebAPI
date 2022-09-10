using Core.DataAccess.NHibernate;
using DataAccess.Abstract;
using Entities.Concrete;
using NHibernate;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataAccess.Concrete.NHibernate
{
    public class NhContainerDal : MapperSession<Container>, IContainerDal
    {
        // With the help of given expressions below, Entities in the database are accessed.
        private readonly ISession session;

        public NhContainerDal(ISession session) : base(session)
        {
            this.session = session;
        }

        // Containers are stored in a list.
        public IQueryable<Container> Containers => session.Query<Container>().OrderBy(x => x.Id);

        //The main method is to cluster the containers of the vehicles on the basis of the given cluster number according to their longitude and latitude values.
        public List<List<Container>> GetOptimezedClusteredContainers(long vehicleId, int clusterNumber)
        {
            // The GetRawData method stores the required input values in an array.
            var rawData = GetRawData(vehicleId);
            // The Cluster method returns an array that encodes cluster membership; the array index is the index of a data tuple, and the array cell value is a zero-based cluster ID.
            int[] clustering = Cluster(rawData, clusterNumber);
            // The GetClusteredLocations method returns the longitude and latitude values based on the cluster ID found with the Cluster method.
            List<List<Locations>> clusteredLocations = GetClusteredLocations(rawData, clustering, clusterNumber);
            // The GetClusteredContainers returns the instance of containers associated with the longitude and latitude found above.
            List<List<Container>> clusteredContainers = GetClusteredContainers(clusteredLocations);
            return clusteredContainers;
        }

        // The GetRawData method stores the required input values in an array.
        private double[][] GetRawData(long vehicleId)
        {
            List<Container> containers = Containers.Where(c => c.VehicleId == vehicleId).ToList(); //The containers of a vehicle are found.
            double[][] rawData = new double[containers.Count][]; // The input array is initiated.
            int i = 0;
            foreach (var container in containers) // The longitude and latitude values belonging to each container are added in order to obtain an input array to work on it.
            {
                rawData[i] = new double[] { container.Latitude, container.Longitude };
                i++;
            }
            return rawData;
        }

        /// <summary>
        /// The k-means algorithm requires the number of clusters to be specified in advance. 
        /// The Cluster method returns an array that encodes cluster membership; the array index is the index of a data tuple, and the array cell value is a zero-based cluster ID. 
        /// For example, the demo result is [1 0 2 2 0 1 . . 2], which means data[0] is assigned to cluster 1, data[1] is assigned to cluster 0, data[2] is assigned to cluster 2, and so on.
        /// </summary>
        /// <param name="rawData"> The parameter that holds the longitude and latitude values to be clustered. </param>
        /// <param name="numClusters"> The parameter that stands for the cluster number. </param>
        /// <returns></returns>
        private int[] Cluster(double[][] rawData, int numClusters)
        {
            // Cluster begins by calling a Normalized method that converts raw data such as {65.0, 220.0} into normalized data such as {-0.05, 0.02}.
            double[][] data = Normalized(rawData);
            // The Boolean variable named "changed" tracks whether or not any of the data tuples changed clusters, or equivalently, whether or not the clustering has changed.
            bool changed = true;
            // The Boolean variable "success" indicates whether the means of the clusters were able to be computed.
            bool success = true;
            // The InitClustering method initializes the clustering array by assigning each data tuple to a randomly selected cluster ID.
            int[] clustering = InitClustering(data.Length, numClusters, 0);
            double[][] means = Allocate(numClusters, data[0].Length);
            // Variables ct and maxCount are used to limit the number of times the clustering loop executes, essentially acting as a sanity check.
            int maxCount = data.Length * 10;
            int ct = 0;
            // The clustering loop exits when there's no change to the clustering, or one or more means cannot be computed because doing so would create a situation with no data tuples assigned to some cluster, or when maxCount iterations is reached.
            while (changed == true && success == true && ct < maxCount)
            {
                ++ct;
                success = UpdateMeans(data, clustering, means);
                changed = UpdateClustering(data, clustering, means);
            }
            return clustering;
        }
        /// <summary>
        /// 
        /// In situations where the components of a data tuple have different scales, such as the height-weight demo data, it's usually a good idea to normalize the data. 
        /// The idea is that the component with larger values (weight in the demo) will overwhelm the component with smaller values.
        /// There are several ways to normalize data. This demo uses Gaussian normalization. 
        /// Each raw value v in a column is converted to a normalized value v' by subtracting the arithmetic mean of all the values in the column and then dividing by the standard deviation of the values. 
        /// Normalized values will almost always be between -10 and +10. For raw data -- which follows a bell-shaped distribution -- most normalized values will be between -3 and +3.
        /// 
        /// </summary>
        /// <param name="rawData"> The data that to be normalized. </param>
        /// <returns></returns>
        private double[][] Normalized(double[][] rawData)
        {
            double[][] result = new double[rawData.Length][];
            for (int i = 0; i < rawData.Length; ++i)
            {
                result[i] = new double[rawData[i].Length];
                Array.Copy(rawData[i], result[i], rawData[i].Length);
            }

            for (int j = 0; j < result[0].Length; ++j)
            {
                double colSum = 0.0;
                for (int i = 0; i < result.Length; ++i)
                    colSum += result[i][j];
                double mean = colSum / result.Length;
                double sum = 0.0;
                for (int i = 0; i < result.Length; ++i)
                    sum += (result[i][j] - mean) * (result[i][j] - mean);
                double sd = sum / result.Length;
                for (int i = 0; i < result.Length; ++i)
                    result[i][j] = (result[i][j] - mean) / sd;
            }
            return result;
        }

        /// <summary>
        /// 
        /// Method InitClustering initializes the clustering array by assigning each data tuple to a randomly selected cluster ID. 
        /// The method arbitrarily assigns tuples 0, 1 and 2 to clusters 0, 1 and 2, respectively, so that each cluster is guaranteed to have at least one data tuple assigned to it.
        /// The final clustering result depends to a large extent on how the clustering is initialized. 
        /// There are several approaches to initialization, but the technique presented here is the simplest and works well in practice.
        /// 
        /// </summary>
        /// <param name="numTuples">The parameter that represents the data length. </param>
        /// <param name="numClusters"> The parameter that stands for the cluster number. </param>
        /// <param name="seed"> The Random class uses the seed value as a starting value for the pseudo-random number generation algorithm.</param>
        /// <returns></returns>
        private int[] InitClustering(int numTuples, int numClusters, int seed)
        {
            Random random = new Random(seed);
            int[] clustering = new int[numTuples];
            for (int i = 0; i < numClusters; ++i)
                clustering[i] = i;
            for (int i = numClusters; i < clustering.Length; ++i)
                clustering[i] = random.Next(0, numClusters);
            return clustering;
        }

        /// <summary>
        /// 
        /// The UpdateMeans method computes the cluster  means. 
        /// Computing the mean of a cluster is best explained by example. 
        /// Suppose a cluster has three height-weight tuples: d0 = {64, 110}, d1 = {65, 160}, d2 = {72, 180}. 
        /// The mean of the cluster is computed as {(64+65+72)/3, (110+160+180)/3} = {67.0, 150.0}. 
        /// In other words, you just compute the average of each data component.
        /// 
        /// </summary>
        /// <param name="data"> The parameter that represent the data to be clustered. </param>
        /// <param name="clustering"> The parameter that holds the cluster ID of each data. </param>
        /// <param name="means"> The parameters that stands for the means of each cluster. </param>
        /// <returns></returns>

        private bool UpdateMeans(double[][] data, int[] clustering, double[][] means)
        {
            int numClusters = means.Length;
            int[] clusterCounts = new int[numClusters];
            for (int i = 0; i < data.Length; ++i)
            {
                int cluster = clustering[i];
                ++clusterCounts[cluster];
            }

            for (int k = 0; k < numClusters; ++k)
                if (clusterCounts[k] == 0)
                    return false;

            for (int k = 0; k < means.Length; ++k)
                for (int j = 0; j < means[k].Length; ++j)
                    means[k][j] = 0.0;

            for (int i = 0; i < data.Length; ++i)
            {
                int cluster = clustering[i];
                for (int j = 0; j < data[i].Length; ++j)
                    means[cluster][j] += data[i][j]; // accumulate sum
            }

            for (int k = 0; k < means.Length; ++k)
                for (int j = 0; j < means[k].Length; ++j)
                    means[k][j] /= clusterCounts[k]; // danger of div by 0
            return true;
        }

        /// <summary>
        /// 
        /// One of the potential pitfalls of the k-means algorithm is that all clusters must have at least one tuple assigned at all times. 
        /// The first few lines of UpdateMeans scan the clustering input array parameter and count the number of tuples assigned to each cluster. 
        /// If any cluster has no tuples assigned, the method exits and returns false. 
        /// This is a fairly expensive operation and can be omitted if the methods that initialize the clustering and update the clustering both guarantee that there are no zero-count clusters.
        /// Notice that matrix means is actually used as a C# style ref parameter -- the new means are stored into the parameter. 
        /// So you might want to label the means parameter with the ref keyword to make this idea explicit.
        /// In method Cluster, for convenience, the means matrix is allocated using helper method allocate.
        /// 
        /// </summary>
        /// <param name="numClusters"> The parameter that stands for the cluster number. </param>
        /// <param name="numColumns"> The parameter that represent the dimension of the data. </param>
        /// <returns></returns>
        private double[][] Allocate(int numClusters, int numColumns)
        {
            double[][] result = new double[numClusters][];
            for (int k = 0; k < numClusters; ++k)
                result[k] = new double[numColumns];
            return result;
        }

        /// <summary>
        /// 
        /// In each iteration of the Cluster method, after new cluster means have been computed, the cluster membership of each data tuple is updated in method UpdateClustering. 
        /// 
        /// </summary>
        /// <param name="data"> The parameter that represent the data to be clustered. </param>
        /// <param name="clustering"> The parameter that holds the cluster ID of each data. </param>
        /// <param name="means"> The parameters that stands for the means of each cluster. </param>
        /// <returns></returns>
        private bool UpdateClustering(double[][] data, int[] clustering, double[][] means)
        {
            int numClusters = means.Length;
            bool changed = false;

            int[] newClustering = new int[clustering.Length];
            Array.Copy(clustering, newClustering, clustering.Length);

            double[] distances = new double[numClusters];

            for (int i = 0; i < data.Length; ++i)
            {
                for (int k = 0; k < numClusters; ++k)
                    distances[k] = Distance(data[i], means[k]);

                int newClusterID = MinIndex(distances);
                if (newClusterID != newClustering[i])
                {
                    changed = true;
                    newClustering[i] = newClusterID;
                }
            }

            if (changed == false)
                return false;

            int[] clusterCounts = new int[numClusters];
            for (int i = 0; i < data.Length; ++i)
            {
                int cluster = newClustering[i];
                ++clusterCounts[cluster];
            }

            for (int k = 0; k < numClusters; ++k)
                if (clusterCounts[k] == 0)
                    return false;

            Array.Copy(newClustering, clustering, newClustering.Length);
            return true; // no zero-counts and at least one change
        }

        /// <summary>
        /// 
        /// Method UpdateClustering uses the idea of the distance between a data tuple and a cluster mean. 
        /// The Euclidean distance between two vectors is the square root of the sum of the squared differences between corresponding component values. 
        /// For example, suppose some data tuple d0 = {68, 140} and three cluster means are c0 = {66.0, 120.0}, c1 = {69.0, 160.0}, and c2 = {70.0, 130.0}. (Note that I'm using raw, un-normalized data for demonstration purposes only.) 
        /// The distance between d0 and c0 = sqrt((68 - 66.0)^2 + (140 - 120.0)^2) = 20.10. 
        /// The distance between d0 and c1 = sqrt((68 - 69.0)^2 + (140 - 160.0)^2) = 20.22. 
        /// And the distance between d0 and c2 = sqrt((68 - 70.0)^2 + (140 - 130.0)^2) = 10.20. 
        /// The data tuple is closest to mean c2, and so would be assigned to cluster 2.
        /// 
        /// </summary>
        /// <param name="tuple"> The parameter that holds the values of each data. </param>
        /// <param name="mean"> The parameter that represent the cluster means. </param>
        /// <returns></returns>
        private double Distance(double[] tuple, double[] mean)
        {
            double sumSquaredDiffs = 0.0;
            for (int j = 0; j < tuple.Length; ++j)
                sumSquaredDiffs += Math.Pow((tuple[j] - mean[j]), 2);
            return Math.Sqrt(sumSquaredDiffs);
        }

        /// <summary>
        /// 
        /// Method UpdateClustering scans each data tuple, computes the distances from the current tuple to each of the cluster means, and then assigns the tuple to the closest mean using helper function MinIndex.
        /// 
        /// </summary>
        /// <param name="distances"> The parameter that represent the the distances between each data and the cluster means. </param>
        /// <returns></returns>
        private int MinIndex(double[] distances)
        {
            int indexOfMin = 0;
            double smallDist = distances[0];
            for (int k = 0; k < distances.Length; ++k)
            {
                if (distances[k] < smallDist)
                {
                    smallDist = distances[k];
                    indexOfMin = k;
                }
            }
            return indexOfMin;
        }

        // This class is created in order to obtain all the given properties all at once.
        private class Locations
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public int clusterId { get; set; }
        }

        /// <summary>
        /// 
        /// The GetClusteredLocations method returns the longitude and latitude values based on the cluster ID.
        ///  
        /// </summary>
        /// <param name="data"> The parameter that represent the data to be clustered. </param>
        /// <param name="clustering"> The parameter that holds the cluster ID of each data. </param>
        /// <param name="numClusters"> The parameter that stands for the cluster number. </param>
        /// <returns></returns>
        private List<List<Locations>> GetClusteredLocations(double[][] data, int[] clustering,int numClusters)
        {
            List<Locations> locationList = new List<Locations>();
            List<List<Locations>> result = new List<List<Locations>>();

            for (int k = 0; k < numClusters; ++k)
            {
                for (int i = 0; i < data.Length; ++i)
                {
                    int clusterID = clustering[i];
                    if (clusterID != k)
                    {
                        continue;
                    }
                    else
                    {
                        Locations location = new Locations();
                        location.Latitude = data[i][0];
                        location.Longitude = data[i][1];
                        location.clusterId = clusterID;
                        locationList.Add(location);
                    }
                }
            }
            result = locationList.GroupBy(l => l.clusterId)
                .Select(grp => grp.ToList())
                .ToList();
            return result;
        }

        /// <summary>
        /// 
        /// The GetClusteredContainers returns the instance of containers associated with the longitude and latitude.
        /// 
        /// </summary>
        /// <param name="locations"> The parameter that holds latitude, longitude and clusterId all at once. </param>
        /// <returns></returns>
        private List<List<Container>> GetClusteredContainers(List<List<Locations>> locations)
        {
            List <Container> instantList = new List<Container>();
            List < List < Container >> resultList = new List<List <Container>>();

            foreach (var location in locations)
            {
                foreach (var item in location)
                {
                    Container container = new Container();
                    container = Containers.Where(c => c.Latitude == item.Latitude && c.Longitude == item.Longitude).FirstOrDefault();
                    instantList.Add(container);
                }
                resultList.Add(instantList.ToList());
                instantList.Clear();
            }
            return resultList;
        }
    }
}
