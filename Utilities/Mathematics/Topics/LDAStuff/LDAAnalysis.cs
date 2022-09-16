﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Utilities.Mathematics.Topics.LDAStuff
{
    public class WordProbability : IComparable
    {
        public double prob;
        public int word;

        public WordProbability(double prob, int word)
        {
            this.prob = prob;
            this.word = word;
        }

        public int CompareTo(object other_obj)
        {
            WordProbability other = other_obj as WordProbability;
            return -prob.CompareTo(other.prob);
        }
    }

    public class DocProbability : IComparable
    {
        public double prob;
        public int doc;

        public DocProbability(double prob, int doc)
        {
            this.prob = prob;
            this.doc = doc;
        }

        public int CompareTo(object other_obj)
        {
            DocProbability other = other_obj as DocProbability;
            return -prob.CompareTo(other.prob);
        }

        public override string ToString()
        {
            return String.Format("{0} ({1})", doc, prob);
        }
    }

    public class TopicProbability : IComparable
    {
        public double prob;
        public int topic;

        public TopicProbability(double prob, int topic)
        {
            this.prob = prob;
            this.topic = topic;
        }

        public int CompareTo(object other_obj)
        {
            TopicProbability other = other_obj as TopicProbability;
            return -prob.CompareTo(other.prob);
        }

        public override string ToString()
        {
            return String.Format("{0} ({1})", topic, prob);
        }
    }

    public class LDAAnalysis
    {
        private LDASampler lda;

        public LDAAnalysis(LDASampler lda)
        {
            this.lda = lda;
        }

        public int NUM_DOCS => lda.NUM_DOCS;

        public int NUM_TOPICS => lda.NUM_TOPICS;

        public int NUM_WORDS => lda.NUM_WORDS;

        private float[,] _density_of_words_in_topics; // [topic,word]
        public float[,] DensityOfWordsInTopics // [topic,word]
        {
            get
            {
                if (null == _density_of_words_in_topics)
                {
                    //try
                    //{
                        Logging.Info("+Generating density_of_words_in_topics");
                        _density_of_words_in_topics = new float[lda.NUM_TOPICS, lda.NUM_WORDS];

                        Parallel.For(0, lda.NUM_TOPICS, (topic) =>
                        //for (int topic = 0; topic < lda.NUM_TOPICS; ++topic)
                        {
                            for (int word = 0; word < lda.NUM_WORDS; ++word)
                            {
                                _density_of_words_in_topics[topic, word] =
                                    lda.number_of_times_a_topic_has_a_specific_word[topic, word] / lda.number_of_times_a_topic_has_any_word[topic];

                                //density_of_words_in_topics[topic, word] =
                                //    (lda.number_of_times_a_topic_has_a_specific_word[topic, word] + lda.BETA) /
                                //    (lda.number_of_times_a_topic_has_any_word[topic] + lda.NUM_WORDS * lda.BETA);
                            }
                        });

                        Logging.Info("-Generating density_of_words_in_topics");
                    //}
                    //catch (System.OutOfMemoryException ex)
                    //{
                    //    // terminate app
                    //    throw;
                    //}
                }

                return _density_of_words_in_topics;
            }
        }

        private float[,] _density_of_topics_in_documents; // [doc,topic]
        public float[,] DensityOfTopicsInDocuments // [doc,topic]
        {
            get
            {
                if (null == _density_of_topics_in_documents)
                {
                    //try
                    //{
                        Logging.Info("+Generating density_of_topics_in_documents");
                        _density_of_topics_in_documents = new float[lda.NUM_DOCS, lda.NUM_TOPICS];

                        Parallel.For(0, lda.NUM_DOCS, (doc) =>
                        //for (int doc = 0; doc < lda.NUM_DOCS; ++doc)
                        {
                            for (int topic = 0; topic < lda.NUM_TOPICS; ++topic)
                            {
                                _density_of_topics_in_documents[doc, topic] =
                                    lda.number_of_times_doc_has_a_specific_topic[doc, topic] / lda.number_of_times_a_doc_has_any_topic[doc];

                                //density_of_topics_in_documents[doc, topic] =
                                //    (lda.number_of_times_doc_has_a_specific_topic[doc, topic] + lda.ALPHA) /
                                //    (lda.number_of_times_a_doc_has_any_topic[doc] + lda.NUM_TOPICS * lda.ALPHA);
                            }
                        });
                        Logging.Info("-Generating density_of_topics_in_documents");
                    //}
                    //catch (System.OutOfMemoryException ex)
                    //{
                    //    // terminate app
                    //    throw;
                    //}
                }

                return _density_of_topics_in_documents;
            }
        }

        private float[,] _pseudo_density_of_topics_in_words; // [word,topic]   // pseudo because I haven't yet derived a statistical basis for this measure
        public float[,] PseudoDensityOfTopicsInWords // [word,topic]
        {
            get
            {
                if (null == _pseudo_density_of_topics_in_words)
                {
                    //try
                    //{
                        Logging.Info("+Generating pseudo_density_of_topics_in_words");
                        _pseudo_density_of_topics_in_words = new float[lda.NUM_WORDS, lda.NUM_TOPICS];
                        for (int word = 0; word < lda.NUM_WORDS; ++word)
                        {
                            float denominator = 0;
                            for (int topic = 0; topic < lda.NUM_TOPICS; ++topic)
                            {
                                denominator += lda.number_of_times_a_topic_has_a_specific_word[topic, word];

                                //denominator += lda.number_of_times_a_topic_has_a_specific_word[topic, word] + lda.BETA;
                            }

                            for (int topic = 0; topic < lda.NUM_TOPICS; ++topic)
                            {
                                _pseudo_density_of_topics_in_words[word, topic] =
                                    lda.number_of_times_a_topic_has_a_specific_word[topic, word] / denominator;

                                //pseudo_density_of_topics_in_words[word, topic] =
                                //    (lda.number_of_times_a_topic_has_a_specific_word[topic, word] + lda.BETA) / denominator;
                            }
                        }

                        Logging.Info("+Generating pseudo_density_of_topics_in_words");
                    //}
                    //catch (System.OutOfMemoryException ex)
                    //{
                    //    // terminate app
                    //    throw;
                    //}
                }

                return _pseudo_density_of_topics_in_words;
            }
        }

        [NonSerialized]
        private WordProbability[][] density_of_words_in_topics_sorted; // [topic][word]
        public WordProbability[][] DensityOfWordsInTopicsSorted // [topic][word]
        {
            get
            {
                // Build this if we need to
                if (null == density_of_words_in_topics_sorted)
                {
                    //try
                    //{
                        // Work out the sorted ranks
                        density_of_words_in_topics_sorted = new WordProbability[lda.NUM_TOPICS][];
                        for (int topic = 0; topic < lda.NUM_TOPICS; ++topic)
                        {
                            density_of_words_in_topics_sorted[topic] = new WordProbability[lda.NUM_WORDS];
                            for (int word = 0; word < lda.NUM_WORDS; ++word)
                            {
                                density_of_words_in_topics_sorted[topic][word] = new WordProbability(DensityOfWordsInTopics[topic, word], word);
                            }
                            Array.Sort(density_of_words_in_topics_sorted[topic]);
                        }
                    //}
                    //catch (System.OutOfMemoryException ex)
                    //{
                    //    // terminate app
                    //    throw;
                    //}
                }

                return density_of_words_in_topics_sorted;
            }
        }

        private DocProbability[][] density_of_docs_in_topics_sorted; // [topic][doc]
        /// <summary>
        /// [topic][doc]
        /// </summary>
        public DocProbability[][] DensityOfDocsInTopicsSorted // [topic][doc]
        {
            get
            {
                // Build this if we need to
                if (null == density_of_docs_in_topics_sorted)
                {
                    //try
                    //{
                        // Work out the sorted ranks
                        density_of_docs_in_topics_sorted = new DocProbability[lda.NUM_TOPICS][];
                        for (int topic = 0; topic < lda.NUM_TOPICS; ++topic)
                        {
                            density_of_docs_in_topics_sorted[topic] = new DocProbability[lda.NUM_DOCS];
                            for (int doc = 0; doc < lda.NUM_DOCS; ++doc)
                            {
                                density_of_docs_in_topics_sorted[topic][doc] = new DocProbability(DensityOfTopicsInDocuments[doc, topic], doc);
                            }
                            Array.Sort(density_of_docs_in_topics_sorted[topic]);
                        }
                    //}
                    //catch (System.OutOfMemoryException ex)
                    //{
                    //    // terminate app
                    //    throw;
                    //}
                }

                return density_of_docs_in_topics_sorted;
            }
        }

        private TopicProbability[][] density_of_topics_in_docs_sorted; // [doc][n]
        /// <summary>
        /// [doc][n]
        /// </summary>
        public TopicProbability[][] DensityOfTopicsInDocsSorted // [doc][n]
        {
            get
            {
                // Build this if we need to
                if (null == density_of_topics_in_docs_sorted)
                {
                    // Work out the sorted ranks
                    density_of_topics_in_docs_sorted = CalculateDensityOfTopicsInDocsSorted(0);
                }

                return density_of_topics_in_docs_sorted;
            }
        }

        private TopicProbability[][] density_of_top5_topics_in_docs_sorted; // [doc][n<5]
        /// <summary>
        /// [doc][n<5]
        /// </summary>
        public TopicProbability[][] DensityOfTop5TopicsInDocsSorted // [doc][n<5]
        {
            get
            {
                // Build this if we need to
                if (null == density_of_top5_topics_in_docs_sorted)
                {
                    // Work out the sorted ranks
                    density_of_top5_topics_in_docs_sorted = CalculateDensityOfTopicsInDocsSorted(5);
                }

                return density_of_top5_topics_in_docs_sorted;
            }
        }

        private TopicProbability[][] CalculateDensityOfTopicsInDocsSorted(int max_topics_to_retain)
        {
            //try
            //{
                TopicProbability[][] local_density_of_topics_in_docs_sorted = new TopicProbability[lda.NUM_DOCS][];

                // How many topics will we remember for each doc?
                int topics_to_retain = max_topics_to_retain;
                if (topics_to_retain <= 0) topics_to_retain = lda.NUM_TOPICS;
                else if (topics_to_retain > lda.NUM_TOPICS) topics_to_retain = lda.NUM_TOPICS;

                // Calculate the density
                float[,] densityoftopicsindocuments = DensityOfTopicsInDocuments;
                Parallel.For(0, lda.NUM_DOCS, (doc) =>
                //for (int doc = 0; doc < lda.NUM_DOCS; ++doc)
                {
                    TopicProbability[] density_of_topics_in_doc = new TopicProbability[lda.NUM_TOPICS];

                    for (int topic = 0; topic < lda.NUM_TOPICS; ++topic)
                    {
                        density_of_topics_in_doc[topic] = new TopicProbability(densityoftopicsindocuments[doc, topic], topic);
                    }
                    Array.Sort(density_of_topics_in_doc);

                    // Copy the correct number of items to retain
                    if (topics_to_retain == lda.NUM_TOPICS)
                    {
                        local_density_of_topics_in_docs_sorted[doc] = density_of_topics_in_doc;
                    }
                    else
                    {
                        local_density_of_topics_in_docs_sorted[doc] = new TopicProbability[topics_to_retain];
                        Array.Copy(density_of_topics_in_doc, local_density_of_topics_in_docs_sorted[doc], topics_to_retain);
                    }
                });

                return local_density_of_topics_in_docs_sorted;
            //}
            //catch (System.OutOfMemoryException ex)
            //{
            //    // terminate app
            //    throw;
            //}
        }

        private TopicProbability[][] density_of_topics_in_docs_scaled_sorted; // [doc][topic]
        /// <summary>
        /// [doc][topic]
        /// </summary>
        public TopicProbability[][] DensityOfTopicsInDocsScaledSorted // [doc][topic]
        {
            get
            {
                // Build this if we need to
                if (null == density_of_topics_in_docs_scaled_sorted)
                {
                    //try
                    //{
                        // This hold how much each topic is used in all the documents
                        double[] total_density_of_topics_in_docs = new double[lda.NUM_TOPICS];
                        for (int topic = 0; topic < lda.NUM_TOPICS; ++topic)
                        {
                            for (int doc = 0; doc < lda.NUM_DOCS; ++doc)
                            {
                                total_density_of_topics_in_docs[topic] += DensityOfTopicsInDocuments[doc, topic];
                            }
                        }

                        // Work out the sorted ranks
                        density_of_topics_in_docs_scaled_sorted = new TopicProbability[lda.NUM_DOCS][];

                        // For each doc
                        for (int doc = 0; doc < lda.NUM_DOCS; ++doc)
                        {
                            density_of_topics_in_docs_scaled_sorted[doc] = new TopicProbability[lda.NUM_TOPICS];

                            // Get the initial density (sums to unity)
                            for (int topic = 0; topic < lda.NUM_TOPICS; ++topic)
                            {
                                density_of_topics_in_docs_scaled_sorted[doc][topic] = new TopicProbability(DensityOfTopicsInDocuments[doc, topic], topic);
                            }

                            // Scale each topic density down by the number of docs that use the topic (the more docs that use the topic, the less the topic is weighted)
                            for (int topic = 0; topic < lda.NUM_TOPICS; ++topic)
                            {
                                density_of_topics_in_docs_scaled_sorted[doc][topic].prob /= total_density_of_topics_in_docs[topic];
                            }

                            // Normalise the column again
                            double total = 0;
                            for (int topic = 0; topic < lda.NUM_TOPICS; ++topic)
                            {
                                total += density_of_topics_in_docs_scaled_sorted[doc][topic].prob;
                            }
                            for (int topic = 0; topic < lda.NUM_TOPICS; ++topic)
                            {
                                density_of_topics_in_docs_scaled_sorted[doc][topic].prob /= total;
                            }

                            Array.Sort(density_of_topics_in_docs_scaled_sorted[doc]);
                        }
                    //}
                    //catch (System.OutOfMemoryException ex)
                    //{
                    //    // terminate app
                    //    throw;
                    //}
                }

                return density_of_topics_in_docs_scaled_sorted;
            }
        }
    }
}
