//using UnityEngine;
//using Firebase;
//using Firebase.Database;
//using Firebase.Extensions;

///// <summary>
///// Manages the initialization of Firebase and handles basic read/write 
///// operations to the Firebase Realtime Database for testing purposes.
///// </summary>
//public class FirebaseTest : MonoBehaviour
//{
//    private DatabaseReference dbReference;

//    private void Start()
//    {
//        InitializeFirebase();
//    }

//    /// <summary>
//    /// Checks and fixes any missing Firebase dependencies before initializing the database connection.
//    /// </summary>
//    private void InitializeFirebase()
//    {
//        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
//        {
//            if (task.Result == DependencyStatus.Available)
//            {
//                Debug.Log("Firebase dependencies resolved successfully. Database ready.");
//                dbReference = FirebaseDatabase.DefaultInstance.RootReference;

//                // Execute initial database read/write tests
//                WritePlayerScore("JeanTesting2", 1997);
//                ReadPlayerScore("JeanTesting2");
//            }
//            else
//            {
//                Debug.LogError($"Could not resolve all Firebase dependencies: {task.Result}");
//            }
//        });
//    }

//    /// <summary>
//    /// Writes a player's score to the database under the 'players' node.
//    /// </summary>
//    /// <param name="playerId">The unique identifier for the player.</param>
//    /// <param name="score">The score value to write.</param>
//    public void WritePlayerScore(string playerId, int score)
//    {
//        if (dbReference == null) return;

//        dbReference.Child("players").Child(playerId).Child("score").SetValueAsync(score);
//        Debug.Log($"Write operation requested for {playerId} with score: {score}.");
//    }

//    /// <summary>
//    /// Retrieves a player's score from the database and logs the result to the Unity Console.
//    /// </summary>
//    /// <param name="playerId">The unique identifier for the player whose score is being retrieved.</param>
//    public void ReadPlayerScore(string playerId)
//    {
//        if (dbReference == null) return;

//        dbReference.Child("players").Child(playerId).Child("score").GetValueAsync().ContinueWithOnMainThread(task =>
//        {
//            if (task.IsFaulted)
//            {
//                Debug.LogError($"Failed to read data for {playerId}: {task.Exception}");
//                return;
//            }

//            if (task.IsCompleted)
//            {
//                DataSnapshot snapshot = task.Result;
                
//                if (snapshot.Exists)
//                {
//                    Debug.Log($"Successfully retrieved score for {playerId}: {snapshot.Value}");
//                }
//                else
//                {
//                    Debug.LogWarning($"Read operation completed, but no data exists for {playerId}.");
//                }
//            }
//        });
//    }
//}