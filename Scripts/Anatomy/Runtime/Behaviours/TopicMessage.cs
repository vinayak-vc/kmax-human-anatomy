namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// What a topic's behaviour wants the caption to say: a heading, a body, an extra line and a line of progress. The key
    /// names the message, so saying the same thing again does not restart the caption's fade.
    /// </summary>
    public class TopicMessage {
        public TopicMessage(string key, string heading, string body, string fact, string progress) {
            Key = key;
            Heading = heading;
            Body = body;
            Fact = fact;
            Progress = progress;
        }

        public string Key { get; private set; }
        public string Heading { get; private set; }
        public string Body { get; private set; }
        public string Fact { get; private set; }
        public string Progress { get; private set; }
    }
}