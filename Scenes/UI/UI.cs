using Godot;
using System.Linq;
using System.Collections.Generic;

public class UI : Control
{   //Objects (Functionality from outside this class)
    Master master; //Changes player control state
    LineEdit playerLine; //To change the Line Edit Text
    Font playerFont; //To measure font, requires currently used font
    ItemList itemList; //To display predictive text
    WordTrie wordTrie; //To predict text (Can also store new words)

    //In Godot there are multiple processes that code is transfered between.
    //I took a Functional Programming Approach for this script.
    //Caution 1
    //Because functions are reusable if I were to mutate these variables in them
    //Then use the functions in the wrong order the code would break.
    //Since variables in 1 function depend on variables from another.

    //Caution 2
    //If a variable gets set at a particular event. It shouldn't change until
    //That event happens again. If you reuse the function that sets it
    //In a place other than than where it's set in the event the code will also
    //break. Regardless if it's in the same process or another process.

    //To prevent that I made a mutation rules for functions that:

    //1 Are self contained with the event where mutation should happen
    //For example setText() should change caret position every single time.
    //By doing that you can record the recent caret position even if the player
    //Moves it without typing.

    //2 Aren't self contained and could be reused and changed by mistake.
    //In this case there is a Mutation Option on those functions that can be
    //Switched on and off to say whether this was the intended call to set those
    //Variables or if it wasn't. The only time originWord should be changed is
    //When something new is typed for example. findCurWord() changes it upon
    //text change which is cool but when findCurWord is called in _Input()
    //It should not be changed so you switch the MO to off.
    
    //Central Structures (No M.O. because they change only when they should.
    List<string> predictedText; //By cycle I mean the next time the player types
    List<string> currentLine; //We store what the player types in here
    int caret; //MO Not set.
    string selectedWord = ""; //Only used when _Input() needs it.

    //Mutating Variables (M.O. because they could change when they shouldn't)
    string originWord = ""; //MO Set, could be set in findCurWord()
    int oriWordInx = 0; //MO Set, could be set in findCurWord()

    //Class Utility
    [Export] bool debug = false;

    //==================GODOT VIRTUALS======================
    public override void _Ready(){
        master = GetTree().Root.GetNode<Master>("Master");
        playerLine = (LineEdit)GetNode("PlayerLine");
        playerFont = playerLine.GetFont(""); //Needs actual current font
        itemList = (ItemList)GetNode("PlayerLine/ItemList");
        //itemList.Hide();
        itemList.Visible = false;
        wordTrie = GetNode<WordTrie>("WordTrie");
        string[] words = new string[]{"i", "a", "may", "pizza", "food", "love", "lose",
        "let", "lets", "loves", "lover", "give", "me", "have", "no", "yes", "want", 
        "need", "desire", "never", "look", "loss", "loser", "loveliest", "else", "tell", "me",
        "know", "feel", "hunger", "table", "worship", "stay", "with", "my", "loveliest", "lovely",
        "kill", "killing", "kills", "harsh", "kind", "great", "create", "move", "sister", "brother",
        "mom", "dad", "heal", "cherish", "nevermind", "master", "lord", "break", "romance", "happy",
        "sad", "fear", "up", "down", "depressed", "angry", "worried", "anxious", "scared", "at", "you",
        "go", "get", "know", "forever", "cat", "hate", "worship", "pray", "ritual", "culture",
        "nice", "fuel", "fuse", "forget", "remember", "memory", "open", "petty", "pity", "observation" };
        for(int i = 0; i < words.Length; i++){
            wordTrie.insert(words[i]);
        }
    }

    public override void _Process(float delta){
        input();
    }
    //Entry Handler
    public override void _Input(InputEvent @event){
        if (@event is InputEventKey key){
            if(itemList.Visible) {
                if(key.IsActionPressed("ui_up")) {
                    int selected = predictedText.Count - 1;
                    List<string> lineWords = currentLine;
                    for(var i = 0; i < predictedText.Count; i++){
                        if(itemList.IsSelected(i)){
                            selected = i;
                        }
                    }
                    if(selected - 1 < 0){
                        itemList.Select(predictedText.Count - 1);
                        selected = predictedText.Count - 1;
                    } else {
                        itemList.Select(selected - 1);
                        selected = - 1;
                    }
                    selectedWord = predictedText[selected];
                    //Doesn't work yet
                    /*lineWords[oriWordInx] = selectedWord;
                    setText(singleSpace(lineWords), caret,
                    spaceCounter(lineWords, caret));*/
                    GetTree().SetInputAsHandled();

                } else if(key.IsActionPressed("ui_down")) {
                    int selected = predictedText.Count - 1;
                    List<string> lineWords = currentLine;
                    for(var i = 0; i < predictedText.Count; i++){
                        if(itemList.IsSelected(i)){
                            selected = i;
                        }
                    }
                    if(selected + 1 >= predictedText.Count){
                        itemList.Select(0);
                        selected = 0;
                    } else {
                        itemList.Select(selected + 1);
                        selected += 1;
                    }
                    selectedWord = predictedText[selected];
                    /*lineWords[oriWordInx] = selectedWord;
                    setText(singleSpace(lineWords), caret,
                    spaceCounter(lineWords, caret));*/
                    GetTree().SetInputAsHandled();
                } 
                itemList.EnsureCurrentIsVisible();
            }
        }
    }
    //Process Input Map -> Finishing Input Handler
    public void input(){
        if (Input.IsActionJustPressed("interact")){
            playerLine.GrabFocus();
            if(master.player.playState != Character.PLAYSTATE.TEXT) {
                master.player.playState = Character.PLAYSTATE.TEXT;
            }
        }
        if(Input.IsActionJustPressed("ui_cancel")){
            playerLine.ReleaseFocus();
            if(master.player.playState != Character.PLAYSTATE.PHYSICS) {
                master.player.playState = Character.PLAYSTATE.PHYSICS;
            }
        }
    }
    //=================Signal Methods=================
    public void _on_PlayerLine_text_changed(string text){
        caret = playerLine.CaretPosition; //Reset Global Caret
        if(text.Length > 0){
            if(caret > 0 && text[caret-1] == ' '){
                return;
            }
            currentLine = text.Split(" ").ToList();
            setText(singleSpace(currentLine), caret,
                    spaceCounter(currentLine, caret));
            string[] results = findCurWord(currentLine, true);
            searchTrie(results[0]);
            setItemListPos(results[1].ToInt());
        }
    }

    public void _on_PlayerLine_text_entered(string text) {
        if(debug == true) {
            debugTrie(text);
        }
    }

    public void _on_PlayerLine_text_change_rejected() {

    }
    //Changed Text Signal
    public int spaceCounter(List<string> lineWords, int prevCaret){
        int spacesTillCaret = 0;
        string lineText = playerLine.Text;
        bool space = true;
        bool startCounting = true; //Skips the first space after each word
        int countInd = 0;

        if(lineWords.Count > 0){//Depends on currentline not being empty
            //Count the text
            for(var i = 0; i < lineText.Length; i++){
                if(lineText[i] != ' ' && space == true){
                    //wordCount[countInd] = i;
                    space = false;
                } else if(lineText[i] == ' ' && space == false) {
                    space = true;
                    startCounting = true;//Skip the first space after each word
                    countInd++;
                } else if(startCounting == true) { //Only count beginning spaces and 2 spaces after each word.
                    if(space == true && i < prevCaret) {
                        spacesTillCaret++;
                    }
                }
            }
        }
        return spacesTillCaret;
    }
    public string singleSpace(List<string> lineWords, int prevCaret = -1) {
        string text = "";
        string lineText = playerLine.Text;
        for(var i = 0; i < lineWords.Count; i++){
            if(i == 0){
                text += lineWords[i];//My  Lov
            } else {
                text += " " + lineWords[i];
            }
        }
        if(lineText[lineText.Length - 1] == ' '){
            text += " ";
        }
        return text;
    }

    public void setText(string nextText, int prevCaret, int removedSpaces){
        //Reset the text
        playerLine.Text = nextText;
        playerLine.CaretPosition = prevCaret - removedSpaces;
        caret = playerLine.CaretPosition; //Record current caret for the whole class
    }
    //findCurWord is Not fully FP yet since it mutates originWord and sentenceLengthEtc
    public string[] findCurWord(List<string> lineWords, bool mutate = false) {
        string currentWord = "";
        List<int> wordCount = new List<int>();
        int tillWordAtCaret = 0;
                                        //From outside but for now it's ok.
        if(lineWords.Count > 0){//Depends on lineWords not being empty
            //Find the caret position and check what word it's on.
            for(var i = 0; i < lineWords.Count; i++){
                if(tillWordAtCaret + lineWords[i].Length + 1 < caret){
                    tillWordAtCaret += lineWords[i].Length + 1; //+1 counts for 1 separating space
                } else {
                    currentWord = lineWords[i];//To return to other functions
                    if(mutate) { //If I called this function for a word that
                    //wasn't the original I'd mess up the originWord variable.
                    //That's why not mututing external stuff in functions is
                    //so important.
                        originWord = lineWords[i];//Also store for whole class
                        oriWordInx = i;//Remember the Index for the class as well
                    }
                    break;
                }
            }
        }
        return new string[]{currentWord, tillWordAtCaret.ToString()};
    }

    public void setItemListPos(int tillWordAtCaret) {
        Vector2 textToCaretSize = new Vector2();
        //Move Item List to the word that the caret is on.
        textToCaretSize = playerFont.GetStringSize(playerLine.Text.Substr(0,
                                                tillWordAtCaret));
        itemList.RectPosition = new Vector2(textToCaretSize.x + 1,
                                            itemList.RectPosition.y);
    }

    public void searchTrie(string searchWord){
        //Search Trie for the word the user is currently changing text on.
        predictedText = wordTrie.search(searchWord);
        itemList.Clear();
        if(predictedText == null) {
            GD.Print("No searchable word follows from player's text.");
            //itemList.Hide();
            itemList.Visible = false;
        } else {
            //GD.Print("Search: ", searchWord);
            for(var i = predictedText.Count - 1; i >= 0; i--){
                //GD.Print("Searched: ", predictedText[i]);
                //itemList.Show();
                itemList.Visible = true;
                itemList.AddItem(predictedText[i], null, true);
            }
            if(searchWord != predictedText[predictedText.Count - 1]) {
                itemList.AddItem(searchWord + "~");
                predictedText.Add(searchWord + "~");
            }
            itemList.Select(predictedText.Count - 1);
            itemList.EnsureCurrentIsVisible();
        }
    }
    //Entered Text Signal
    public void debugTrie(string text) {
        if(text.Length > 0){
            currentLine = text.Split(" ").OfType<string>().ToList();
            if(currentLine.Count > 1){
                if(currentLine[0] == "Insert") {
                    wordTrie.insert(currentLine[1]);
                    GD.Print("Inserted: ", currentLine[1]);
                }
                if(currentLine[0] == "Find") {
                    GD.Print("Found? ", currentLine[1], " = ", wordTrie.find( currentLine[1]));
                }
                if(currentLine[0] == "Delete") {
                    wordTrie.remove(currentLine[1]);
                }
                if(currentLine[0] == "Search") {
                    List<string> predictedText = wordTrie.search(currentLine[1]);
                    if(predictedText == null) {
                        GD.Print("No searchable word follows from player's text.");
                    } else {
                        GD.Print("Search: ", currentLine[1]);
                        for(var i = predictedText.Count - 1; i >= 0; i--){
                            GD.Print("Searched: ", predictedText[i]);
                            itemList.AddItem(predictedText[i], null, true);
                        }
                    }
                }
            }
        }
    }
}

/*//Vector2 textSize = Vector2.Zero; //Originally intended to limit
        //How much the user can type by checking via the StringSize
        //Rather than by the amount of characters.

        //textSize = playerFont.GetStringSize(playerLine.Text);
        
        //Yikes we might have to word count how many letters to
        //delete by calling the size over and over again. Deleting
        from the end wouldn't be cool though. I need a new algorithm that
        //doesn't limit spaces and instead checks for the spot where the
        //user last typed at and instead deletes that character. Idk if
        //there is some sort of glitch they can do to make it multiple
        //characters past size though lol */


/*                    selectedWord = predictedText[selected];
                    string selectedText = newText.Substr(0, sentenceLengthTillWordAtCaret-1);
                    selectedText += selectedWord;
                    selectedText += newText.Substr(sentenceLengthTillWordAtCaret +
                                    selectedWord.Length - 1, newText.Length - 1);
                    newText = selectedText;
                    playerLine.Text = selectedText;
                    playerLine.CaretPosition = sentenceLengthTillWordAtCaret + selectedWord.Length - 1;
                    
                    selectedWord = predictedText[selected];
                    string selectedText = newText.Substr(0, sentenceLengthTillWordAtCaret-1);
                    selectedText += selectedWord;
                    selectedText += newText.Substr(sentenceLengthTillWordAtCaret +
                                    selectedWord.Length - 1, newText.Length - 1);
                    newText = selectedText;
                    playerLine.Text = selectedText;
                    playerLine.CaretPosition = sentenceLengthTillWordAtCaret + selectedWord.Length - 1;*/

                    
    /*  string newText = nextText;
        int caretSpace = 0;
            public string includePrecedingSpace(string nextText, int prevCaret) {
        string lineText = playerLine.Text;
        string newText = "";
        // if(prevCaret != 1 && lineText[0] != ' '){
            
        // }
        
        // if((nextText.Length == prevCaret &&
        //             lineText[nextText.Length - 1] == ' ') ||
        //                 (nextText.Length + 1 == prevCaret) &&
        //                     lineText[nextText.Length] == ' ') {
                                
        // }
        return newText;
    }
        if(allowCaretSpace == true) {
            newText = includePrecedingSpace(nextText, prevCaret);
            if(newText.Length > nextText.Length) {
                caretSpace++;
            } 
        } 
    
    
    includeStartingSpace(lineText, prevCaret)
        
        else if(i != lineWords.Count - 1 &&
                includePrecedingSpace(text, lineText, prevCaret)) { 
                    text += "  " + lineWords[i];
            }*/