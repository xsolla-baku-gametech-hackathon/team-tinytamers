using PetPal.Api.Common;
using PetPal.Api.Entities;
using PetPal.Shared.Enums;
using static PetPal.Api.Data.Questions.QuestionBank;

namespace PetPal.Api.Data.Questions;

/// <summary>
/// İngilis dilində sual bankı: hər bacarıqda 50 sual, hər çətinlikdə 5.
///
/// Məntiq, oxu və elm bölmələri <see cref="AzQuestionBank"/> ilə paraleldir —
/// eyni uşaq dili dəyişəndə eyni çətinliyi görməlidir. Söz ehtiyatı isə
/// paralel DEYİL: orfoqrafiya, prefiks və omofonlar ingilis dilinə xasdır.
/// </summary>
internal static class EnQuestionBank
{
    private const string En = Localized.English;

    public static IEnumerable<Question> All() =>
        [.. Vocabulary(), .. Logic(), .. Reading(), .. Science()];

    // ---------------- Vocabulary ----------------

    private static IEnumerable<Question> Vocabulary() =>
    [
        Choice(En, SkillArea.Vocabulary, 1, "Which word means the same as 'big'?", ["huge", "tiny", "slow", "cold"], 0, "'Huge' and 'big' both describe great size.", "Think about size."),
        Choice(En, SkillArea.Vocabulary, 1, "Which word is the opposite of 'hot'?", ["warm", "cold", "sunny", "dry"], 1, "The opposite of hot is cold.", "Think about ice."),
        Choice(En, SkillArea.Vocabulary, 1, "Which word is the opposite of 'night'?", ["morning", "evening", "day", "tomorrow"], 2, "The opposite of night is day.", "When can you see the sun?"),
        Choice(En, SkillArea.Vocabulary, 1, "Which word names an animal?", ["table", "spoon", "window", "rabbit"], 3, "A rabbit is an animal; the others are objects.", "Which one is alive?"),
        Choice(En, SkillArea.Vocabulary, 1, "Which word is the opposite of 'fast'?", ["long", "slow", "heavy", "hot"], 1, "The opposite of fast is slow.", "How does a tortoise move?"),

        Choice(En, SkillArea.Vocabulary, 2, "A baby cat is called a…", ["puppy", "kitten", "cub", "foal"], 1, "A baby cat is a kitten.", "Think about cats."),
        Choice(En, SkillArea.Vocabulary, 2, "Which word means 'happy'?", ["joyful", "angry", "tired", "afraid"], 0, "'Joyful' means full of happiness.", "Which one feels good?"),
        Choice(En, SkillArea.Vocabulary, 2, "A baby dog is called a…", ["foal", "lamb", "puppy", "chick"], 2, "A baby dog is a puppy.", "Think about dogs."),
        Choice(En, SkillArea.Vocabulary, 2, "Which word names a colour?", ["notebook", "quickly", "run", "green"], 3, "'Green' is a colour.", "Which one can you see?"),
        Choice(En, SkillArea.Vocabulary, 2, "Which word is the opposite of 'clean'?", ["dry", "dirty", "hot", "narrow"], 1, "The opposite of clean is dirty.", "Think about mud."),

        Choice(En, SkillArea.Vocabulary, 3, "Which word is a verb (an action)?", ["table", "quickly", "jump", "blue"], 2, "'Jump' is something you do, so it is a verb.", "Which word can you do?"),
        Choice(En, SkillArea.Vocabulary, 3, "Which word means 'very small'?", ["gigantic", "tiny", "heavy", "loud"], 1, "'Tiny' means very small.", "Think of an ant."),
        Choice(En, SkillArea.Vocabulary, 3, "A baby hen is called a…", ["chick", "lamb", "puppy", "foal"], 0, "A baby hen is a chick.", "Who hatches from an egg?"),
        Choice(En, SkillArea.Vocabulary, 3, "Which word shows more than one?", ["apple", "apples", "apple's", "appled"], 1, "The ending -s makes a plural.", "Look at the ending."),
        Choice(En, SkillArea.Vocabulary, 3, "Which word is the opposite of 'light' (in weight)?", ["narrow", "long", "heavy", "hot"], 2, "The opposite of light is heavy.", "Think of lifting a rock."),

        Choice(En, SkillArea.Vocabulary, 4, "Choose the correct spelling.", ["freind", "friend", "frend", "friennd"], 1, "The correct spelling is 'friend'.", "Remember: i before e in 'friend'."),
        Choice(En, SkillArea.Vocabulary, 4, "Which word is a noun?", ["run", "softly", "green", "river"], 3, "'River' names a thing, so it is a noun.", "Which one is a thing?"),
        Choice(En, SkillArea.Vocabulary, 4, "In 'boxes', which part shows there is more than one?", ["box", "-es", "-x", "b-"], 1, "The ending -es makes the plural.", "Look at the ending."),
        Choice(En, SkillArea.Vocabulary, 4, "Which spelling is correct?", ["becuase", "becouse", "because", "becaus"], 2, "The correct spelling is 'because'.", "Say it in parts: be-cause."),
        Choice(En, SkillArea.Vocabulary, 4, "Which word is the opposite of 'deep'?", ["shallow", "wide", "long", "tall"], 0, "The opposite of deep is shallow.", "Think of the edge of a river."),

        Choice(En, SkillArea.Vocabulary, 5, "'Brave' means…", ["not afraid", "very tired", "very rich", "very quiet"], 0, "A brave person is not afraid.", "Think of a hero."),
        Choice(En, SkillArea.Vocabulary, 5, "Which word completes it: 'The soup was so ___ that I burned my tongue.'", ["cool", "sweet", "hot", "empty"], 2, "Only something hot burns your tongue.", "What burns?"),
        Choice(En, SkillArea.Vocabulary, 5, "Which word names a job?", ["yesterday", "doctor", "quickly", "book"], 1, "A doctor is a job.", "Who works at a hospital?"),
        Choice(En, SkillArea.Vocabulary, 5, "Which is the past tense of 'go'?", ["goed", "gone", "went", "going"], 2, "The past tense of 'go' is 'went'.", "Yesterday I ___ home."),
        Choice(En, SkillArea.Vocabulary, 5, "Which word is the opposite of 'empty'?", ["full", "open", "light", "clean"], 0, "The opposite of empty is full.", "Picture a glass."),

        Choice(En, SkillArea.Vocabulary, 6, "Which word is an adjective?", ["shiny", "sing", "school", "slowly"], 0, "'Shiny' describes a thing, so it is an adjective.", "Which word describes?"),
        Choice(En, SkillArea.Vocabulary, 6, "'Ancient' means…", ["very new", "very fast", "very old", "very small"], 2, "'Ancient' means extremely old.", "Think of pyramids."),
        Choice(En, SkillArea.Vocabulary, 6, "What is the root word in 'unhappy'?", ["un-", "happy", "-py", "unhap"], 1, "'Happy' is the root; 'un-' is added to it.", "Take off the beginning."),
        Choice(En, SkillArea.Vocabulary, 6, "Which word is an adverb (it tells how)?", ["car", "red", "child", "quickly"], 3, "'Quickly' tells how something is done.", "Ask: how?"),
        Choice(En, SkillArea.Vocabulary, 6, "What does the prefix 'un-' mean in 'unlock'?", ["more", "not or opposite", "again", "before"], 1, "'Un-' reverses the meaning.", "Unlock is the opposite of lock."),

        Choice(En, SkillArea.Vocabulary, 7, "Which pair are synonyms?", ["begin – start", "begin – stop", "begin – slow", "begin – close"], 0, "'Begin' and 'start' mean the same thing.", "Which two mean the same?"),
        Choice(En, SkillArea.Vocabulary, 7, "How many words make up 'sunflower'?", ["one", "two", "three", "four"], 1, "It is 'sun' plus 'flower'.", "Split the word in half."),
        Choice(En, SkillArea.Vocabulary, 7, "What does 'break the ice' mean?", ["start a conversation", "cut some ice", "feel cold", "get angry"], 0, "It means making a first friendly step.", "Idioms are not read word by word."),
        Choice(En, SkillArea.Vocabulary, 7, "Which word has a suffix?", ["teacher", "teach", "chair", "stone"], 0, "'Teacher' is teach + -er.", "Which one has an ending added?"),
        Choice(En, SkillArea.Vocabulary, 7, "'Rare' means…", ["hard to find", "very common", "very big", "very cheap"], 0, "Something rare is not often found.", "What is hard to find?"),

        Choice(En, SkillArea.Vocabulary, 8, "'Curious' describes someone who…", ["is always angry", "wants to know things", "never speaks", "runs fast"], 1, "A curious person loves to find things out.", "Think about asking questions."),
        Choice(En, SkillArea.Vocabulary, 8, "'Patient' means…", ["gets angry quickly", "talks a lot", "able to wait calmly", "always tired"], 2, "A patient person can wait without fuss.", "Think about a long queue."),
        Choice(En, SkillArea.Vocabulary, 8, "Which pair are homophones (they sound the same)?", ["their – there", "their – then", "three – tree", "thin – think"], 0, "'Their' and 'there' sound alike but mean different things.", "Say them out loud."),
        Choice(En, SkillArea.Vocabulary, 8, "Which word comes from 'write'?", ["wrist", "writer", "wrong", "wrap"], 1, "A writer is someone who writes.", "Who does the writing?"),
        Choice(En, SkillArea.Vocabulary, 8, "'Modest' describes someone who…", ["does not boast", "talks a lot", "is angry", "is lazy"], 0, "A modest person does not show off.", "What is the opposite of boasting?"),

        Choice(En, SkillArea.Vocabulary, 9, "Which word means 'to make something better'?", ["ignore", "improve", "reduce", "repeat"], 1, "'Improve' means to make better.", "Think about progress."),
        Choice(En, SkillArea.Vocabulary, 9, "'Result' means…", ["the beginning", "the cause", "what comes out at the end", "a question"], 2, "A result is what you end up with.", "Think about the end of a match."),
        Choice(En, SkillArea.Vocabulary, 9, "'Hypothesis' means…", ["an idea still to be tested", "a proven law", "an official report", "a maths formula"], 0, "A hypothesis is an idea a scientist tests.", "What does a scientist start with?"),
        Choice(En, SkillArea.Vocabulary, 9, "'Reliable' describes someone who…", ["keeps their word", "runs fast", "is funny", "is rich"], 0, "You can depend on a reliable person.", "Who can you count on?"),
        Choice(En, SkillArea.Vocabulary, 9, "How many suffixes are in 'carelessness'?", ["one", "two", "three", "four"], 1, "care + less + ness: two suffixes.", "Find the root, then count."),

        Choice(En, SkillArea.Vocabulary, 10, "'Reluctant' means…", ["excited", "hungry", "unwilling", "confused"], 2, "A reluctant person does not really want to do it.", "Think about hesitating."),
        Choice(En, SkillArea.Vocabulary, 10, "'Dispute' means…", ["a disagreement", "full agreement", "a long journey", "a big gift"], 0, "In a dispute people do not agree.", "What is the opposite of agreement?"),
        Choice(En, SkillArea.Vocabulary, 10, "'Thrifty' describes someone who…", ["wastes money", "spends carefully", "is lazy", "is angry"], 1, "A thrifty person spends with care.", "What is the opposite of wasteful?"),
        Choice(En, SkillArea.Vocabulary, 10, "'Sooner or later' means…", ["never", "right now", "at some point for sure", "by accident"], 2, "It means it will certainly happen one day.", "Think about time."),
        Choice(En, SkillArea.Vocabulary, 10, "Which idea is abstract (you cannot touch it)?", ["table", "stone", "tree", "love"], 3, "Love cannot be seen or held.", "Which one cannot be picked up?"),
    ];

    // ---------------- Logic ----------------

    private static IEnumerable<Question> Logic() =>
    [
        Choice(En, SkillArea.Logic, 1, "What comes next? 2, 4, 6, ___", ["7", "8", "9", "10"], 1, "The pattern adds 2 each time.", "Look at the gap between numbers."),
        Choice(En, SkillArea.Logic, 1, "Which one is different?", ["apple", "banana", "carrot", "pear"], 2, "Carrot is a vegetable; the rest are fruits.", "Three of them are fruits."),
        Choice(En, SkillArea.Logic, 1, "What comes next? 1, 2, 3, ___", ["4", "5", "6", "7"], 0, "The numbers go up by one.", "Just keep counting."),
        Choice(En, SkillArea.Logic, 1, "Which one does not belong with the others?", ["table", "dog", "cat", "horse"], 0, "A table is not alive; the rest are animals.", "Three of them breathe."),
        Choice(En, SkillArea.Logic, 1, "Which shape has no corners?", ["square", "triangle", "circle", "rectangle"], 2, "A circle has no corners.", "Trace the edge with your finger."),

        Choice(En, SkillArea.Logic, 2, "What comes next? 5, 10, 15, ___", ["20", "22", "25", "30"], 0, "The pattern adds 5 each time.", "Count in fives."),
        Choice(En, SkillArea.Logic, 2, "Which shape has 3 sides?", ["square", "triangle", "circle", "hexagon"], 1, "A triangle has exactly 3 sides.", "'Tri' means three."),
        Choice(En, SkillArea.Logic, 2, "What comes next? 10, 9, 8, ___", ["7", "8", "9", "11"], 0, "The numbers go down by one.", "Count backwards."),
        Choice(En, SkillArea.Logic, 2, "Which word does not fit with the rest?", ["red", "blue", "green", "square"], 3, "Square is a shape; the rest are colours.", "Three of them are colours."),
        Choice(En, SkillArea.Logic, 2, "Which shape has 4 equal sides?", ["triangle", "circle", "square", "oval"], 2, "A square has four equal sides.", "Count the sides."),

        Choice(En, SkillArea.Logic, 3, "If today is Monday, what day is tomorrow?", ["Sunday", "Tuesday", "Friday", "Monday"], 1, "The day after Monday is Tuesday.", "Say the days in order."),
        Choice(En, SkillArea.Logic, 3, "What comes next? A, C, E, ___", ["F", "G", "H", "I"], 1, "The pattern skips one letter each time.", "Count letters two by two."),
        Choice(En, SkillArea.Logic, 3, "What comes next? 10, 20, 30, ___", ["35", "40", "45", "50"], 1, "The pattern adds 10 each time.", "Count in tens."),
        Choice(En, SkillArea.Logic, 3, "How many days are in a week?", ["5", "6", "7", "8"], 2, "A week has 7 days.", "List the days."),
        Choice(En, SkillArea.Logic, 3, "If yesterday was Wednesday, what day is today?", ["Thursday", "Tuesday", "Friday", "Sunday"], 0, "The day after Wednesday is Thursday.", "Move one day forward."),

        Choice(En, SkillArea.Logic, 4, "Ali is taller than Sara. Sara is taller than Nina. Who is tallest?", ["Ali", "Sara", "Nina", "Cannot tell"], 0, "Ali is above Sara, who is above Nina.", "Put them in a line."),
        Choice(En, SkillArea.Logic, 4, "What comes next? 1, 2, 4, 8, ___", ["16", "18", "20", "24"], 0, "Each number doubles.", "Try multiplying by 2."),
        Choice(En, SkillArea.Logic, 4, "What comes next? 3, 6, 9, ___", ["12", "13", "15", "18"], 0, "The pattern adds 3 each time.", "Count in threes."),
        Choice(En, SkillArea.Logic, 4, "Kamil is older than Ay. Ay is older than Nur. Who is youngest?", ["Kamil", "Ay", "Nur", "Cannot tell"], 2, "Nur is below everyone else.", "Look at the end of the line."),
        Choice(En, SkillArea.Logic, 4, "Which number does not fit? 2, 4, 6, 9, 8", ["2", "6", "8", "9"], 3, "The list is even numbers, but 9 is odd.", "Find the even numbers."),

        Choice(En, SkillArea.Logic, 5, "All cats have tails. Mia is a cat. So Mia…", ["has a tail", "has wings", "is a dog", "cannot be known"], 0, "If all cats have tails and Mia is a cat, Mia has a tail.", "Follow the rule."),
        Choice(En, SkillArea.Logic, 5, "Which number does not belong? 3, 5, 8, 7", ["3", "5", "8", "7"], 2, "8 is the only even number.", "Look for odd and even."),
        Choice(En, SkillArea.Logic, 5, "All birds have feathers. Tuti is a bird. So Tuti…", ["has feathers", "has scales", "has four legs", "has no tail"], 0, "The rule applies to all birds.", "Apply the rule to Tuti."),
        Choice(En, SkillArea.Logic, 5, "What comes next? 20, 17, 14, ___", ["10", "11", "12", "13"], 1, "The pattern takes away 3 each time.", "Work out the gap."),
        Choice(En, SkillArea.Logic, 5, "Which item is different from the others?", ["boot", "sock", "hat", "spoon"], 3, "A spoon is not clothing.", "Three of them are worn."),

        Choice(En, SkillArea.Logic, 6, "What comes next? 3, 6, 12, 24, ___", ["26", "30", "36", "48"], 3, "Each number doubles.", "Try multiplying by 2."),
        Choice(En, SkillArea.Logic, 6, "What comes next? 1, 4, 9, 16, ___", ["17", "20", "24", "25"], 3, "These are 1×1, 2×2, 3×3, 4×4, 5×5.", "Multiply each number by itself."),
        Choice(En, SkillArea.Logic, 6, "A box holds 4 pairs of socks. How many socks is that?", ["4", "6", "8", "12"], 2, "A pair is 2, so 4 pairs is 8.", "'Pair' means two."),
        Choice(En, SkillArea.Logic, 6, "What comes next? 2, 3, 5, 8, 12, ___", ["13", "15", "16", "17"], 3, "The gaps grow: 1, 2, 3, 4, 5.", "Write down the gaps."),
        Choice(En, SkillArea.Logic, 6, "Which statement is always true?", ["All squares are rectangles", "All rectangles are squares", "All circles are squares", "All triangles are equal"], 0, "A square is a special kind of rectangle.", "Which one holds every time?"),

        Choice(En, SkillArea.Logic, 7, "A box has 3 red balls and 2 blue balls. You take 1 ball without looking. Which colour is more likely?", ["red", "blue", "same chance", "impossible to say"], 0, "There are more red balls, so red is more likely.", "Count each colour."),
        Choice(En, SkillArea.Logic, 7, "A dice has 6 faces. What is the chance of rolling a 7?", ["very high", "small", "impossible", "fifty-fifty"], 2, "There is no 7 on a dice, so it cannot happen.", "Count the faces."),
        Choice(En, SkillArea.Logic, 7, "What comes next? 1, 3, 6, 10, ___", ["11", "13", "14", "15"], 3, "The gaps grow: 2, 3, 4, 5.", "Write down the gaps."),
        Choice(En, SkillArea.Logic, 7, "A bag holds only red balls. You take one out. What colour is it?", ["red", "blue", "cannot tell", "green"], 0, "There is nothing else in the bag.", "What is in the bag?"),
        Choice(En, SkillArea.Logic, 7, "Ayla is faster than Rashid. Rashid is faster than Jamila. Who is slowest?", ["Ayla", "Rashid", "Jamila", "Cannot tell"], 2, "Jamila is slower than everyone.", "Look at the end of the line."),

        Choice(En, SkillArea.Logic, 8, "What comes next? 1, 1, 2, 3, 5, ___", ["8", "9", "10", "12"], 0, "Each number is the sum of the two before it.", "Add the last two numbers."),
        Choice(En, SkillArea.Logic, 8, "What comes next? 64, 32, 16, ___", ["4", "6", "8", "12"], 2, "Each number is halved.", "Take half each time."),
        Choice(En, SkillArea.Logic, 8, "Of five friends, three play football and two play chess. How many more play football?", ["one", "two", "three", "five"], 0, "3 − 2 = 1.", "Work out the difference."),
        Choice(En, SkillArea.Logic, 8, "If 'all fish swim' is true, then 'all swimmers are fish' is…", ["true", "false", "the same thing", "impossible to tell"], 1, "People swim too, and they are not fish.", "Who else swims?"),
        Choice(En, SkillArea.Logic, 8, "I watered every flower, yet one flower wilted. So…", ["water alone is not enough", "I watered none of them", "all of them wilted", "water is harmful"], 0, "Light or soil may also matter.", "What else does a plant need?"),

        Choice(En, SkillArea.Logic, 9, "If it rains, the ground gets wet. The ground is dry. So…", ["it rained", "it did not rain", "it will rain", "we cannot tell"], 1, "Dry ground means it did not rain.", "Work the rule backwards."),
        Choice(En, SkillArea.Logic, 9, "Every book read earns one sticker. Aydin has 4 stickers. How many books did he read?", ["2", "3", "4", "8"], 2, "Each sticker stands for one book.", "Match them one to one."),
        Choice(En, SkillArea.Logic, 9, "What comes next? 2, 6, 12, 20, ___", ["21", "24", "28", "30"], 3, "The gaps grow: 4, 6, 8, 10.", "Write down the gaps."),
        Choice(En, SkillArea.Logic, 9, "Every blue box has a toy inside. This box has no toy. So the box…", ["is not blue", "must be blue", "is both blue and empty", "cannot be known"], 0, "If it were blue it would hold a toy.", "Work the rule backwards."),
        Choice(En, SkillArea.Logic, 9, "Aygun plays before Kamil, and Kamil plays before Nur. Who plays second?", ["Aygun", "Kamil", "Nur", "Cannot tell"], 1, "The order is Aygun, Kamil, Nur.", "Line all three up."),

        Choice(En, SkillArea.Logic, 10, "5 machines make 5 toys in 5 minutes. How long do 10 machines need for 10 toys?", ["5 minutes", "10 minutes", "20 minutes", "1 minute"], 0, "Each machine makes 1 toy in 5 minutes, so the time stays the same.", "Think about one machine."),
        Choice(En, SkillArea.Logic, 10, "Lilies double every day and cover the lake on day 20. On which day is the lake half covered?", ["day 10", "day 15", "day 18", "day 19"], 3, "It doubles on the last day, so the day before it is half.", "Step back one day."),
        Choice(En, SkillArea.Logic, 10, "Reading 3 pages takes 6 minutes. How long do 10 pages take?", ["12", "16", "20", "30"], 2, "One page takes 2 minutes, so 10 pages take 20.", "Find one page first."),
        Choice(En, SkillArea.Logic, 10, "Two fathers and two sons caught 3 fish — one each. How is that possible?", ["One person is both a father and a son", "One of them caught nothing", "One caught two fish", "It is impossible"], 0, "Grandfather, father and son make three people.", "One person can hold two roles."),
        Choice(En, SkillArea.Logic, 10, "A brick weighs 1 kg plus half a brick. How heavy is the brick?", ["1", "1.5", "2", "3"], 2, "Half a brick is 1 kg, so a whole brick is 2 kg.", "Find the half first, then double it."),
    ];

    // ---------------- Reading ----------------

    private static IEnumerable<Question> Reading() =>
    [
        Choice(En, SkillArea.Reading, 1, "'Aya has a small white cat. Its name is Snow.' What is the cat called?", ["Aya", "Small", "White", "Snow"], 3, "The text says the cat is called Snow.", "Read the sentence again."),
        Choice(En, SkillArea.Reading, 1, "'Elvin rides his red bicycle.' What colour is the bicycle?", ["blue", "green", "red", "yellow"], 2, "The text says the bicycle is red.", "Find the colour word."),
        Choice(En, SkillArea.Reading, 1, "'Narmin planted a flower in the garden.' Where did she plant it?", ["at school", "in the garden", "at home", "in a shop"], 1, "The sentence says 'in the garden'.", "Find the place word."),
        Choice(En, SkillArea.Reading, 1, "'The little dog sleeps under the table.' Where does the dog sleep?", ["on the table", "under the table", "in a bed", "in the yard"], 1, "The dog is under the table.", "Look at the word 'under'."),
        Choice(En, SkillArea.Reading, 1, "'In the morning Kamran drank milk.' What did Kamran drink?", ["water", "tea", "milk", "juice"], 2, "The text says he drank milk.", "Find what he drank."),

        Choice(En, SkillArea.Reading, 2, "'Max the fox found a shiny leaf near the river.' Where was the leaf?", ["in a cave", "on the roof", "near the river", "under the bed"], 2, "The text says the leaf was near the river.", "Read the sentence again."),
        Choice(En, SkillArea.Reading, 2, "'Leyla loves playing ball. Every evening she goes out to the yard.' What does Leyla play?", ["chess", "ball", "cards", "hide and seek"], 1, "The first sentence says ball.", "Look at the first sentence."),
        Choice(En, SkillArea.Reading, 2, "'It was raining, so the children played indoors.' Why did they play indoors?", ["it was cold", "it was late", "there was no ball", "it was raining"], 3, "The reason is stated in the text.", "Look before the word 'so'."),
        Choice(En, SkillArea.Reading, 2, "'My grandfather built me a tree house. It is very strong.' Who built the tree house?", ["my father", "my grandfather", "my uncle", "our neighbour"], 1, "The first sentence names the grandfather.", "Find who did it."),
        Choice(En, SkillArea.Reading, 2, "'When the sky went dark, the stars appeared.' When did the stars appear?", ["in the morning", "at noon", "when the sky went dark", "in the rain"], 2, "The stars came out as the sky darkened.", "Find the time phrase."),

        Choice(En, SkillArea.Reading, 3, "'Lina packed her bag, put on her boots and walked to school.' What did Lina do first?", ["put on boots", "walked to school", "ate breakfast", "packed her bag"], 3, "Packing the bag is mentioned first.", "Look at the order of actions."),
        Choice(En, SkillArea.Reading, 3, "'Aysel washed her hands first, then kneaded the dough, and last baked biscuits.' What did she do last?", ["washed hands", "kneaded dough", "baked biscuits", "set the table"], 2, "The word 'last' marks the final action.", "Find the word 'last'."),
        Choice(En, SkillArea.Reading, 3, "'The bell rang, the children entered the class, the teacher said hello.' What happened right after the bell?", ["the teacher said hello", "the children entered", "the lesson ended", "the bell rang again"], 1, "The order is bell, entering, greeting.", "Follow the order with your finger."),
        Choice(En, SkillArea.Reading, 3, "'We planted a seed, watered it, and a week later we saw a green shoot.' When did the shoot appear?", ["at once", "the next day", "a week later", "a month later"], 2, "The text says 'a week later'.", "Find the time phrase."),
        Choice(En, SkillArea.Reading, 3, "'Rashid washed the bike, then oiled it, then rode it.' What did he do just before riding?", ["washed it", "oiled it", "rode it", "put it away"], 1, "He oiled it right before riding.", "Step back one action."),

        Choice(En, SkillArea.Reading, 4, "'The sky turned grey and everyone ran inside.' What probably happened?", ["the sun came out", "it snowed sand", "a party began", "it started to rain"], 3, "Grey sky and running inside suggest rain.", "Use the clues."),
        Choice(En, SkillArea.Reading, 4, "'Narmin did not take her umbrella. Her hair was wet when she got home.' What happened?", ["it snowed", "it rained", "the umbrella was lost", "the wind blew"], 1, "Wet hair points to rain.", "What makes hair wet?"),
        Choice(En, SkillArea.Reading, 4, "'When Kamil opened the door, candles were burning on a cake and everyone was hiding.' What kind of evening is this?", ["a birthday", "a school day", "New Year", "a holiday trip"], 0, "Candles on a cake and a surprise mean a birthday.", "What do candles on a cake mean?"),
        Choice(En, SkillArea.Reading, 4, "'Aylin opened her notebook but had no pen. She asked her neighbour.' What did Aylin ask for?", ["a notebook", "a pen", "a rubber", "a book"], 1, "Only the pen was missing.", "Find what was missing."),
        Choice(En, SkillArea.Reading, 4, "'Everyone on the street wore thick coats and their breath steamed.' What was the weather like?", ["hot", "cold", "rainy", "windy"], 1, "Thick coats and steaming breath mean cold.", "When can you see your breath?"),

        Choice(En, SkillArea.Reading, 5, "'Sam smiled the whole way home after the match.' How did Sam feel?", ["angry", "bored", "scared", "happy"], 3, "Smiling shows Sam felt happy.", "What does a smile mean?"),
        Choice(En, SkillArea.Reading, 5, "'When Aygun stepped onto the stage, her hands were shaking.' How did Aygun feel?", ["nervous", "greedy", "sleepy", "angry"], 0, "Shaking hands show nerves.", "How does a stage feel?"),
        Choice(En, SkillArea.Reading, 5, "'Nur's balloon popped and she cried.' Why did Nur cry?", ["her balloon popped", "she was hungry", "she was tired", "she was running"], 0, "The reason comes right before.", "What happened just before?"),
        Choice(En, SkillArea.Reading, 5, "'Elvin trained all week and won the race.' Why did Elvin win?", ["he was lucky", "he trained a lot", "his rival did not come", "he broke a rule"], 1, "The text links training to winning.", "What did he do before the race?"),
        Choice(En, SkillArea.Reading, 5, "'Leyla gave her own biscuit to her friend.' What does this show about Leyla?", ["mean", "angry", "generous", "shy"], 2, "Sharing shows generosity.", "What is a sharing person like?"),

        Choice(En, SkillArea.Reading, 6, "'The old bridge groaned under the heavy truck.' The word 'groaned' tells us the bridge…", ["made a low sound", "was newly built", "was painted", "was very short"], 0, "'Groaned' describes a low creaking sound.", "Think about the sound of the word."),
        Choice(En, SkillArea.Reading, 6, "'The child whispered excitedly.' What does 'whisper' mean?", ["to shout", "to speak very quietly", "to stay silent", "to laugh"], 1, "A whisper is a very quiet voice.", "How do you tell a secret?"),
        Choice(En, SkillArea.Reading, 6, "'A gigantic wave rocked the boat.' What does 'gigantic' tell us?", ["very small", "very cold", "very fast", "very big"], 3, "'Gigantic' means very big.", "Think about size."),
        Choice(En, SkillArea.Reading, 6, "'The cat glided silently into the room.' What does 'glided' mean here?", ["moved smoothly and quietly", "ran", "fell", "stopped"], 0, "'Glided' describes smooth movement.", "'Silently' is your clue."),
        Choice(En, SkillArea.Reading, 6, "'Nothing could be seen in the murky water.' What does 'murky' mean?", ["clear", "clean", "dirty and hard to see through", "warm"], 2, "Murky water cannot be seen through.", "Why could nothing be seen?"),

        Choice(En, SkillArea.Reading, 7, "A story that teaches a lesson using animals is called a…", ["fable", "recipe", "diary", "report"], 0, "Fables use animals to teach a lesson.", "Think of the tortoise and the hare."),
        Choice(En, SkillArea.Reading, 7, "'First sift the flour, then add an egg, then bake for 20 minutes.' What kind of text is this?", ["a story", "a recipe", "a poem", "a letter"], 1, "Step-by-step instructions make a recipe.", "What does the text teach you to do?"),
        Choice(En, SkillArea.Reading, 7, "What is the MAIN IDEA of a text?", ["its longest sentence", "what the text is mostly about", "its first word", "the author's name"], 1, "The main idea is what the text is mostly about.", "What is it about overall?"),
        Choice(En, SkillArea.Reading, 7, "'Yesterday we went to the zoo. I liked the giraffe most.' What kind of text is this?", ["a diary entry", "a science article", "an advert", "a recipe"], 0, "A personal account is a diary entry.", "Who is speaking?"),
        Choice(En, SkillArea.Reading, 7, "What makes a poem different from prose?", ["it is written in lines", "its length", "its title", "its picture"], 0, "Poems are written in lines and verses.", "How does it look on the page?"),

        Choice(En, SkillArea.Reading, 8, "'Despite the rain, the team kept playing.' What does 'despite' show?", ["a cause", "a time", "a contrast", "a place"], 2, "'Despite' shows something happened against expectation.", "Rain would normally stop play."),
        Choice(En, SkillArea.Reading, 8, "'He could not get up in the morning because he slept late.' What does 'because' show?", ["a time", "a cause", "a place", "a purpose"], 1, "'Because' introduces the reason.", "Why could he not get up?"),
        Choice(En, SkillArea.Reading, 8, "'First he finished his homework, then he went out to play.' What does 'then' show?", ["the order in time", "a cause", "a place", "a number"], 0, "'Then' shows what happens next.", "Which came first?"),
        Choice(En, SkillArea.Reading, 8, "'He brought neither a book nor a notebook.' What does this mean?", ["he brought both", "he brought neither of them", "he brought only a book", "he brought only a notebook"], 1, "'Neither… nor' means none of them.", "Read the negative words."),
        Choice(En, SkillArea.Reading, 8, "'She was tired, but she finished her work.' What does 'but' show?", ["an unexpected result", "a cause", "a time", "a place"], 0, "You would expect a tired person to stop.", "What was expected?"),

        Choice(En, SkillArea.Reading, 9, "'Kamran knew the answer but did not raise his hand. Later he regretted it.' What did Kamran feel at first?", ["glad", "hungry", "angry", "shy"], 3, "Knowing but staying silent shows shyness.", "What held him back?"),
        Choice(En, SkillArea.Reading, 9, "'Aysel hid her friend's picture so the surprise would not be spoiled.' Why did Aysel do it?", ["she disliked the picture", "to protect the surprise", "she was cross with her friend", "the picture was bad"], 1, "The purpose is stated after 'so'.", "Find the purpose."),
        Choice(En, SkillArea.Reading, 9, "'The neighbour knocked and said: \"Your door has been left open.\"' Why had the neighbour come?", ["to warn them", "to visit", "to borrow a key", "to ask for money"], 0, "The neighbour reports something careless.", "What did they say?"),
        Choice(En, SkillArea.Reading, 9, "'The teacher handed back the notebook and said: \"Read more carefully this time.\"' What does this show?", ["there were mistakes in the work", "the work was perfect", "the notebook was lost", "the lesson ended"], 0, "'More carefully' hints at mistakes.", "Why would a teacher say that?"),
        Choice(En, SkillArea.Reading, 9, "'Nur always took the same road, but today she turned down another street and arrived late.' Why was she late?", ["she changed her route", "she left early", "she was running", "she found a shortcut"], 0, "The only thing that changed was the route.", "What was different today?"),

        Choice(En, SkillArea.Reading, 10, "'Brush your teeth twice a day — it prevents toothache.' What is the author's purpose?", ["to give advice", "to entertain", "to tell a story", "to sell something"], 0, "The text tells the reader what to do.", "What does the text want from you?"),
        Choice(En, SkillArea.Reading, 10, "'We have the best bikes! Buy now, 50% off!' What is the purpose of this text?", ["to teach", "to sell", "to tell a story", "to report news"], 1, "A discount and a call to buy make an advert.", "What is the text offering?"),
        Choice(En, SkillArea.Reading, 10, "Retelling a long text in one or two sentences is called a…", ["summary", "title", "description", "dialogue"], 0, "A summary gives the short version.", "What do you call the short version?"),
        Choice(En, SkillArea.Reading, 10, "'Strong winds hit the city yesterday, two trees fell, nobody was hurt.' What kind of text is this?", ["a fairy tale", "a poem", "a recipe", "a news report"], 3, "A short account of a real event is news.", "When did it happen?"),
        Choice(En, SkillArea.Reading, 10, "If a text keeps using 'but' and 'however', what is the author doing?", ["contrasting two ideas", "counting", "describing", "asking questions"], 0, "These words mark contrast.", "What do these words join?"),
    ];

    // ---------------- Science ----------------

    private static IEnumerable<Question> Science() =>
    [
        Choice(En, SkillArea.Science, 1, "Which animal can fly?", ["dog", "fish", "cat", "bird"], 3, "Birds have wings and can fly.", "Look for wings."),
        Choice(En, SkillArea.Science, 1, "What can you see in the sky at night?", ["the sun", "the moon and stars", "a rainbow", "snow"], 1, "The moon and stars are seen at night.", "Look up after dark."),
        Choice(En, SkillArea.Science, 1, "Where do fish live?", ["in trees", "in the air", "in water", "in sand"], 2, "Fish live in water.", "What do you need to swim?"),
        Choice(En, SkillArea.Science, 1, "Which one gives heat?", ["ice", "the sun", "a stone", "the wind"], 1, "The sun gives heat and light.", "What warms us in summer?"),
        Choice(En, SkillArea.Science, 1, "How many eyes does a person have?", ["one", "two", "three", "four"], 1, "People have two eyes.", "Touch your face and count."),

        Choice(En, SkillArea.Science, 2, "What do plants need to grow?", ["sand and glass", "plastic", "metal", "sunlight and water"], 3, "Plants need sunlight and water.", "Think about a garden."),
        Choice(En, SkillArea.Science, 2, "Which one is living?", ["a stone", "a tree", "a table", "a spoon"], 1, "A tree grows, so it is living.", "Which one grows?"),
        Choice(En, SkillArea.Science, 2, "Which animal gives us milk?", ["a hen", "a cow", "a fish", "a bee"], 1, "Milk comes from cows.", "Think about a farm."),
        Choice(En, SkillArea.Science, 2, "Which creature makes honey?", ["an ant", "a bee", "a butterfly", "a fly"], 1, "Bees make honey.", "Think about a hive."),
        Choice(En, SkillArea.Science, 2, "Which one is a kind of weather?", ["rain", "a book", "a table", "a shoe"], 0, "Rain is weather.", "Which one comes from the sky?"),

        Choice(En, SkillArea.Science, 3, "When is a shadow made?", ["when the air gets cold", "when light is blocked by an object", "when the wind blows", "when night falls"], 1, "A shadow appears where an object blocks light.", "Hold your hand up in the sun."),
        Choice(En, SkillArea.Science, 3, "Why is it light during the day?", ["the moon gives light", "the stars burn", "the clouds shine", "the sun gives light"], 3, "Daylight comes from the sun.", "What is the biggest light source?"),
        Choice(En, SkillArea.Science, 3, "Which material is transparent (you can see through it)?", ["glass", "wood", "iron", "cardboard"], 0, "Light passes through glass.", "Look at a window."),
        Choice(En, SkillArea.Science, 3, "In which season does snow fall?", ["summer", "autumn", "winter", "spring"], 2, "Snow falls in winter.", "Which season is coldest?"),
        Choice(En, SkillArea.Science, 3, "What is ice made from?", ["sand", "water", "soil", "air"], 1, "Ice is frozen water.", "Think about a freezer."),

        Choice(En, SkillArea.Science, 4, "Water turns into ice when it gets…", ["hotter", "louder", "dirtier", "colder"], 3, "Water freezes when it gets cold enough.", "Think about a freezer."),
        Choice(En, SkillArea.Science, 4, "What does a thermometer measure?", ["length", "weight", "temperature", "time"], 2, "A thermometer measures temperature.", "What is measured when you are ill?"),
        Choice(En, SkillArea.Science, 4, "Where does rain fall from?", ["the soil", "the clouds", "the sea", "the trees"], 1, "Rain falls from clouds.", "Look up before it rains."),
        Choice(En, SkillArea.Science, 4, "How many seasons are there in a year?", ["two", "three", "four", "five"], 2, "There are four: spring, summer, autumn, winter.", "List the seasons."),
        Choice(En, SkillArea.Science, 4, "Which one is a liquid?", ["a stone", "water", "iron", "wood"], 1, "Water is a liquid — it takes the shape of its container.", "Which one flows?"),

        Choice(En, SkillArea.Science, 5, "Which planet do we live on?", ["Mars", "Venus", "Earth", "Jupiter"], 2, "We live on planet Earth.", "Look at a globe."),
        Choice(En, SkillArea.Science, 5, "What is the Moon to the Earth?", ["a star", "a natural satellite", "a planet", "a cloud"], 1, "The Moon is Earth's natural satellite.", "What does the Moon travel around?"),
        Choice(En, SkillArea.Science, 5, "How many planets are in our solar system?", ["six", "seven", "eight", "nine"], 2, "There are eight planets.", "Count from Mercury to Neptune."),
        Choice(En, SkillArea.Science, 5, "What causes day and night?", ["the Earth spinning on its axis", "the Earth standing still", "the Moon growing", "the clouds"], 0, "As Earth spins, day and night take turns.", "What does the Earth do?"),
        Choice(En, SkillArea.Science, 5, "Which direction does a compass needle point?", ["east", "west", "north", "down"], 2, "A compass points north.", "How do travellers find their way?"),

        Choice(En, SkillArea.Science, 6, "What is the closest star to Earth?", ["the Moon", "Polaris", "Mars", "the Sun"], 3, "The Sun is our closest star.", "It lights up our day."),
        Choice(En, SkillArea.Science, 6, "Which animal is cold-blooded?", ["a dog", "a bird", "a lizard", "a cat"], 2, "A lizard's body temperature follows its surroundings.", "Who warms up in the sun?"),
        Choice(En, SkillArea.Science, 6, "Which animal is a mammal?", ["a fish", "a snake", "a bat", "a frog"], 2, "A bat flies but is still a mammal.", "Who feeds their young with milk?"),
        Choice(En, SkillArea.Science, 6, "What is a butterfly before it becomes a butterfly?", ["a caterpillar", "a bird", "a fish", "an ant"], 0, "Butterflies grow from caterpillars.", "Think about a cocoon."),
        Choice(En, SkillArea.Science, 6, "Which animal sleeps through the winter?", ["a bear", "a horse", "a cow", "a rabbit"], 0, "Bears hibernate in winter.", "Who sleeps all winter?"),

        Choice(En, SkillArea.Science, 7, "Which part of a plant takes in water from the soil?", ["leaf", "flower", "root", "seed"], 2, "Roots absorb water from the soil.", "Look underground."),
        Choice(En, SkillArea.Science, 7, "Which gas do plants release into the air?", ["oxygen", "helium", "nitrogen", "hydrogen"], 0, "Plants release oxygen.", "Why is a forest easy to breathe in?"),
        Choice(En, SkillArea.Science, 7, "In a food chain, an animal that eats only plants is called…", ["a predator", "a herbivore", "a parasite", "a fungus"], 1, "Herbivores feed on plants.", "What does a cow eat?"),
        Choice(En, SkillArea.Science, 7, "Why are leaves green?", ["because of chlorophyll", "because of water", "because the sun is hot", "because soil is green"], 0, "Chlorophyll gives leaves their green colour.", "Which substance is in a leaf?"),
        Choice(En, SkillArea.Science, 7, "What grows out of a seed?", ["a stone", "a new plant", "water", "air"], 1, "A new plant sprouts from a seed.", "What do we plant?"),

        Choice(En, SkillArea.Science, 8, "What gas do humans need to breathe?", ["helium", "nitrogen only", "carbon dioxide", "oxygen"], 3, "Humans breathe in oxygen.", "Think about air."),
        Choice(En, SkillArea.Science, 8, "What does the heart do?", ["digests food", "cleans the air", "moves blood around the body", "strengthens bones"], 2, "The heart pumps blood around the body.", "What beats in your chest?"),
        Choice(En, SkillArea.Science, 8, "What is the skeleton for?", ["it supports and protects the body", "it only feeds us", "it cleans the air", "it gives heat"], 0, "Bones hold us up and protect our organs.", "What would happen without bones?"),
        Choice(En, SkillArea.Science, 8, "Which organ do we hear with?", ["the eye", "the ear", "the nose", "the tongue"], 1, "We hear sound with our ears.", "Which organ catches sound?"),
        Choice(En, SkillArea.Science, 8, "What do the lungs do?", ["take in and let out air", "filter blood", "digest food", "build bones"], 0, "The lungs let us breathe.", "What fills up when you breathe in?"),

        Choice(En, SkillArea.Science, 9, "Water changing into vapour is called…", ["erosion", "gravity", "reflection", "evaporation"], 3, "Evaporation turns liquid water into vapour.", "Think about a boiling kettle."),
        Choice(En, SkillArea.Science, 9, "In the water cycle, what happens before rain?", ["evaporation and cloud forming", "an earthquake", "snow melting", "night falling"], 0, "Water evaporates, forms clouds, then falls as rain.", "How does water get up to the sky?"),
        Choice(En, SkillArea.Science, 9, "How does sound travel?", ["as waves through the air", "only through water", "through empty space", "along a beam of light"], 0, "Sound travels as waves through air.", "What does sound need?"),
        Choice(En, SkillArea.Science, 9, "Which object will a magnet attract?", ["an iron lid", "a plastic spoon", "a wooden ruler", "paper"], 0, "Magnets attract iron.", "Which one is metal?"),
        Choice(En, SkillArea.Science, 9, "When are shadows long?", ["when the sun is low", "when the sun is overhead", "at night", "in the rain"], 0, "Shadows stretch when the sun is near the horizon.", "Look at your shadow in the evening."),

        Choice(En, SkillArea.Science, 10, "Which force pulls objects towards the Earth?", ["friction", "magnetism", "pressure", "gravity"], 3, "Gravity pulls objects towards Earth.", "Why does a ball fall down?"),
        Choice(En, SkillArea.Science, 10, "What does friction do?", ["it slows movement down", "it speeds movement up", "it adds weight", "it makes light"], 0, "Friction resists movement.", "Why do you slip on ice?"),
        Choice(En, SkillArea.Science, 10, "Energy comes in which forms?", ["heat only", "heat, light and movement", "light only", "sound only"], 1, "Energy has several forms.", "What does a lamp give you?"),
        Choice(En, SkillArea.Science, 10, "When does a lunar eclipse happen?", ["when the Earth passes between the Sun and the Moon", "when the Moon enters the Sun", "when nights are long", "in winter"], 0, "Earth's shadow falls on the Moon.", "Whose shadow is it?"),
        Choice(En, SkillArea.Science, 10, "Why can sound not be heard in space?", ["there is no air to carry it", "it is too cold", "it is too far", "it is too dark"], 0, "Sound needs a medium to travel through.", "What does sound travel in?"),
    ];
}
