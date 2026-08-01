using UnityEngine;

public enum Gender { Male, Female }

public static class NameGenerator
{
    private static readonly string[] maleNames = { "John", "Mike", "David", "Chris", "Robert", "James", "Andrew", "Joshua", "Ryan", "Jacob" };
    private static readonly string[] femaleNames = { "Emma", "Olivia", "Sophia", "Ava", "Isabella", "Mia", "Harper", "Lily", "Grace", "Evelyn" };

    public static string GetRandomName(Gender gender)
    {
        return gender == Gender.Male
            ? maleNames[Random.Range(0, maleNames.Length)]
            : femaleNames[Random.Range(0, femaleNames.Length)];
    }
}