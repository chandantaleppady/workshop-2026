// heartbeat - Timer trigger, runs every 5 minutes (schedule is in function.json)
module.exports = async function (context, myTimer) {
    context.log(`Heartbeat: function app is alive at ${new Date().toISOString()}`);
};
