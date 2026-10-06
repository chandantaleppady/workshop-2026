// hello - HTTP trigger (portal-editable style: index.js + function.json)
module.exports = async function (context, req) {
    const name = (req.query && req.query.name) || (req.body && req.body.name) || 'student';
    const greeting = process.env.GREETING || 'Hello';   // from Environment variables (optional)
    context.log(`hello function called for ${name}`);
    context.res = {
        headers: { 'Content-Type': 'application/json' },
        body: {
            message: `${greeting} ${name}, this reply came from Azure Functions!`,
            serverTime: new Date().toISOString()
        }
    };
};
