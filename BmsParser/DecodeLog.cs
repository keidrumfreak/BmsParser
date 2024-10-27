using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BmsParser.DecodeLog;

namespace BmsParser
{
    public class DecodeLog
    {
        private String message;

        private State state;

        public DecodeLog(State state, String message)
        {
            this.message = message;
            this.state = state;
        }

        public State getState()
        {
            return state;
        }

        public String getMessage()
        {
            return message;
        }

        public enum State
        {
            INFO, WARNING, ERROR
        }
    }
}
