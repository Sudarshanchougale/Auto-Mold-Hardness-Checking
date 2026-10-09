namespace HardnessCell.Analysis
{
    public enum QualityVerdict
    {
        /// <summary>Not every required point has a valid reading; neither PASS nor FAIL may be reported.</summary>
        Incomplete,
        Pass,
        Fail,
    }
}
