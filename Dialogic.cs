using System;
using System.IO;
using System.Collections.Generic;


public class Dialogic
{
    public static List<string> _UnlockData = new List<string>();
    private List<ActionFlag> _Flags = new List<ActionFlag>();
    private List<ExecuteRequest> _Requests = new List<ExecuteRequest>();
    private List<DialogContainer> _DialogContainers = new List<DialogContainer>();

    private Picker _Picker = new Picker();

    private int _Step = 0;
    private DialogContainer _CurrentDialog;

    public event Action<string[]> Choices;
    public event Action<string[]> DialogLine;
    public static event Action<string[]> VariableNotify;

    public Dialogic()
    {
        Preparation();
    }

    public void Load(params string[] files)
    {
        for (int i = 0; i < files.Length; i++)
        {
            string[] text = File.ReadAllLines(files[i]);

            string name = "";
            List<string> data = new List<string>();

            for (int j = 0; j < text.Length; j++)
            {
                text[j] = text[j].Replace("\t", "");

                if (text[j] == "")
                    continue;

                if (text[j].StartsWith("@"))
                {
                    if (name != "")
                    {
                        _DialogContainers.Add(new DialogContainer(name, data.ToArray()));
                        data = new List<string>();
                    }

                    name = text[j].Replace("@", "");
                    continue;
                }

                if (j == text.Length - 1 && name != "")
                {
                    _DialogContainers.Add(new DialogContainer(name, data.ToArray()));

                    name = "";
                    data = new List<string>();

                    continue;
                }

                if (name != "")
                {
                    data.Add(text[j]);
                    continue;
                }
                else
                {
                    Console.WriteLine($"Incorrect data. Object outside the body of the dialog on a line: {j}");
                    continue;
                }
            }
        }
    }
    public void AddExecuteRequest(string command, Action<string[]> callback, params string[] separators)
        => _Requests.Add(new ExecuteRequest(command, callback, separators));

    public void Start(string name)
    {
        DialogContainer? dialog = _DialogContainers.Find(x => x.Name == name);

        _Step = 0;

        if (dialog.HasValue && dialog.Value.Name != null && dialog.Value.Data != null)
        {
            _CurrentDialog = dialog.Value;

            Next();
        }
        else
            throw new ArgumentException($"Dialog \"{name}\" not exist.");
    }
    public bool Next()
    {
        if (_Step < _CurrentDialog.Data.Length)
        {
            ProcessingLine(_CurrentDialog.Data[_Step++]);
            return true;
        }

        return false;
    }
    public void Pick(int id)
        => _Picker.PickVariant(this, id);

    public void Lock(string value)
    {
        if (_UnlockData.Exists(x => x == value))
            _UnlockData.RemoveAll(x => x == value);
    }
    public void Unlock(string value)
    {
        if (!_UnlockData.Exists(x => x == value))
            _UnlockData.Add(value);
    }

    private void Preparation()
    {
        _Flags.Add(new ActionFlag(">", ExecuteCommnad, true));
        _Flags.Add(new ActionFlag("#", ExecuteChoice, false));
        _Flags.Add(new ActionFlag("&", ExecuteChoice, false));
        _Flags.Add(new ActionFlag("//", ExecuteComment, true));
        _Flags.Add(new ActionFlag("~>", ExecuteMultiLineCommand, true));
        _Flags.Add(new ActionFlag("~", ExecuteMultiLineDialog, true));

        AddExecuteRequest("Jump", (data) => Start(data[0].Replace(" ", "")));
        AddExecuteRequest("VAR", (data) => VariableNotify.Invoke(data));
        AddExecuteRequest("SOUND", (data) => Console.WriteLine($"Sound: {data[0]}"));
        AddExecuteRequest("ANIMATION", (data) => Console.WriteLine($"Animation: {data[0]}"));
        AddExecuteRequest("UNLOCK", (data) => _UnlockData.Add(data[0].Replace(" ", "")));
    }

    private void ExecuteCommnad(string text)
    {
        string[] commandData = text.Split(':');

        ExecuteRequest? request = _Requests.Find(x => x.Command == commandData[0].Replace(" ", ""));

        if (request.HasValue)
            request.Value.Action?.Invoke(commandData[1].Split('☼', request.Value.Separators, StringSplitOptions.RemoveEmptyEntries));
        else
            Console.WriteLine($"Command \"{commandData[0]}\" not found.");

        Next();
    }
    private void ExecuteChoice(string text)
    {

        List<string> choices = new List<string>();

        choices.Add(text);

        if (_Step < _CurrentDialog.Data.Length)
        {
            for (int i = _Step; i < _CurrentDialog.Data.Length; i++)
            {
                if (_CurrentDialog.Data[i].StartsWith("#") || _CurrentDialog.Data[i].StartsWith("&"))
                    choices.Add(_CurrentDialog.Data[i]);
                else
                    break;
            }
        }

        _Step += choices.Count - 1;

        string[] nextReplica = null;

        if (_Step <= _CurrentDialog.Data.Length - 1)
        {
            bool isDialog = true;

            for (int i = 0; i < _Flags.Count; i++)
                if ((_Flags[i].Value != "~" || _Flags[i].Value == "~>") && _CurrentDialog.Data[_Step].StartsWith(_Flags[i].Value))
                    isDialog = false;

            if (isDialog)
            {
                if (_CurrentDialog.Data[_Step].StartsWith("~"))
                {
                    string name = _CurrentDialog.Data[_Step].Replace("~", "");

                    List<string> data = new List<string>();

                    if (_Step < _CurrentDialog.Data.Length)
                    {
                        for (int i = _Step; i < _CurrentDialog.Data.Length; i++)
                        {
                            if (_CurrentDialog.Data[i].StartsWith(":"))
                                data.Add(_CurrentDialog.Data[i]);
                            else
                                break;
                        }
                    }

                    _Step += data.Count;

                    for (int i = 0; i < data.Count; i++)
                        data[i] = name + data[i];

                    nextReplica = data.ToArray();
                }
                else
                {
                    nextReplica = new string[] { _CurrentDialog.Data[_Step] };

                    _Step += 1;
                }
            }
        }

        List<string> choicesPush = new List<string>();
        List<string> choicesCommand = new List<string>();
        List<string> choicesCurrent = new List<string>();

        for (int i = 0; i < choices.Count; i++)
        {
            if (choices[i].StartsWith("&"))
            {
                string[] data = choices[i].TrimStart('&').Split('☼', ":>");

                string[] dataUnlock = data[0].Split(',');

                bool unlock = false;

                for (int j = 0; j < dataUnlock.Length; j++)
                {
                    if (FindUnlock(data[j].Replace(" ", "")))
                    {
                        unlock = true;
                        break;
                    }
                }

                if (unlock)
                    choicesCurrent.Add(data[1]);
            }
            else
                choicesCurrent.Add(choices[i].TrimStart('#'));
        }


        for (int i = 0; i < choicesCurrent.Count; i++)
        {
            string[] data = choicesCurrent[i].Split('☼', ">>>");
            choicesPush.Add(data[1]);
            choicesCommand.Add(">" + data[0]);
        }

        _Picker.Commands = choicesCommand.ToArray();

        if (nextReplica != null)
            DialogLine?.Invoke(nextReplica);

        Choices?.Invoke(choicesPush.ToArray());
    }
    private void ExecuteComment(string text)
        => Console.WriteLine($"Comment: {text}");
    private void ExecuteMultiLineCommand(string text)
    {
        string command = text;

        List<string> data = new List<string>();

        if (_Step < _CurrentDialog.Data.Length)
        {
            for (int i = _Step; i < _CurrentDialog.Data.Length; i++)
            {
                if (_CurrentDialog.Data[i].StartsWith(":"))
                    data.Add(_CurrentDialog.Data[i].TrimStart(':'));
                else
                    break;
            }
        }

        _Step += data.Count;

        ExecuteRequest? request = _Requests.Find(x => x.Command == command.Replace(" ", ""));

        if (request.HasValue)
            for (int i = 0; i < data.Count; i++)
                request.Value.Action?.Invoke(data[i].Split('☼', request.Value.Separators, StringSplitOptions.RemoveEmptyEntries));
        else
            Console.WriteLine($"Command \"{command}\" not found.");

        Next();
    }
    private void ExecuteMultiLineDialog(string text)
    {
        string name = text;

        List<string> data = new List<string>();

        if (_Step < _CurrentDialog.Data.Length)
        {
            for (int i = _Step; i < _CurrentDialog.Data.Length; i++)
            {
                if (_CurrentDialog.Data[i].StartsWith(":"))
                    data.Add(_CurrentDialog.Data[i]);
                else
                    break;
            }
        }

        _Step += data.Count;

        for (int i = 0; i < data.Count; i++)
            data[i] = name + data[i];

        DialogLine?.Invoke(data.ToArray());
    }

    private bool FindFlag(string text, bool execute = true)
    {
        for (int i = 0; i < _Flags.Count; i++)
            if (text.StartsWith(_Flags[i].Value))
            {
                if (execute)
                    _Flags[i].DoAction(text);

                return true;
            }

        return false;
    }
    private bool FindUnlock(string value)
    {
        for (int i = 0; i < _UnlockData.Count; i++)
            if (_UnlockData[i] == value)
                return true;

        return false;
    }
    private void ProcessingLine(string line)
    {
        if (!FindFlag(line))
            DialogLine?.Invoke(new string[] { line });
    }

    private struct ActionFlag
    {
        private bool _RemoveFlag;
        private Action<string> _Action;

        public string Value;

        public ActionFlag(string value, Action<string> action, bool removeFlag)
        {
            Value = value;

            _Action = action;
            _RemoveFlag = removeFlag;
        }

        public void DoAction(string text)
            => _Action?.Invoke(_RemoveFlag ? text.TrimStart(Value.ToCharArray()) : text);
    }
    private struct Picker
    {
        private bool _IsActive;
        private string[] _Commands;

        public string[] Commands
        {
            set
            {
                _Commands = value;

                if (value != null)
                    _IsActive = true;
            }
        }

        public void PickVariant(Dialogic dialogic, int id)
        {
            if (_IsActive)
            {
                _IsActive = false;
                dialogic.ProcessingLine(_Commands[id]);
            }
            else
                throw new Exception();
        }
    }
    private struct ExecuteRequest
    {
        public string Command;
        public string[] Separators;
        public Action<string[]> Action;

        public ExecuteRequest(string command, Action<string[]> action, string[] separators)
        {
            Command = command;
            Separators = separators;
            Action = action;
        }
    }
    private struct DialogContainer
    {
        public string Name;
        public string[] Data;

        public DialogContainer(string name, string[] data)
        {
            Name = name;
            Data = data;
        }
    }
}
